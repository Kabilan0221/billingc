using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Text;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Data.Database;

namespace ShopBilling.Data.Repositories
{
    public class StockRepository : IStockRepository
    {
        public StockDashboardMetrics GetDashboardMetrics()
        {
            var metrics = new StockDashboardMetrics();

            using (var conn = DatabaseConnection.CreateConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            COUNT(*) AS total_products,
                            COALESCE(SUM(current_stock), 0) AS total_stock_qty,
                            COALESCE(SUM(CASE WHEN current_stock <= 0 THEN 1 ELSE 0 END), 0) AS out_of_stock,
                            COALESCE(SUM(CASE WHEN current_stock > 0 AND current_stock <= reorder_level THEN 1 ELSE 0 END), 0) AS low_stock,
                            COALESCE(SUM(CASE WHEN max_stock_level > 0 AND current_stock > max_stock_level THEN 1 ELSE 0 END), 0) AS excess_stock,
                            COALESCE(SUM(current_stock * purchase_rate), 0) AS stock_value_cost,
                            COALESCE(SUM(current_stock * sale_rate), 0) AS stock_value_sale
                        FROM products
                        WHERE is_active = 1;
                    ";

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            metrics.TotalActiveProducts = Convert.ToInt32(r["total_products"]);
                            metrics.TotalStockQuantity = Convert.ToDecimal(r["total_stock_qty"]);
                            metrics.OutOfStockCount = Convert.ToInt32(r["out_of_stock"]);
                            metrics.LowStockCount = Convert.ToInt32(r["low_stock"]);
                            metrics.ExcessStockCount = Convert.ToInt32(r["excess_stock"]);
                            metrics.StockValueAtCost = Convert.ToDecimal(r["stock_value_cost"]);
                            metrics.StockValueAtSale = Convert.ToDecimal(r["stock_value_sale"]);
                        }
                    }
                }

                // Count distinct products adjusted
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(DISTINCT product_id) FROM stock_adjustment_items;";
                    metrics.AdjustedProductsCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }

                // Recent movements
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT sm.*, p.product_code, p.name_en AS product_name
                        FROM stock_movements sm
                        JOIN products p ON sm.product_id = p.id
                        ORDER BY sm.created_at DESC, sm.id DESC
                        LIMIT 10;
                    ";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            metrics.RecentMovements.Add(new StockMovement
                            {
                                Id = Convert.ToInt64(r["id"]),
                                ProductId = Convert.ToInt64(r["product_id"]),
                                ProductCode = r["product_code"].ToString(),
                                ProductName = r["product_name"].ToString(),
                                ReferenceType = r["reference_type"].ToString(),
                                ReferenceId = r["reference_id"].ToString(),
                                Quantity = Convert.ToDecimal(r["quantity"]),
                                QuantityBefore = Convert.ToDecimal(r["quantity_before"]),
                                QuantityAfter = Convert.ToDecimal(r["quantity_after"]),
                                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                                CreatedAt = Convert.ToDateTime(r["created_at"])
                            });
                        }
                    }
                }
            }

            return metrics;
        }

        public PagedResult<CurrentStockSummary> GetCurrentStock(StockFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var items = new List<CurrentStockSummary>();
            int totalCount = 0;

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var sbWhere = new StringBuilder(" WHERE p.is_active = 1 ");
                var cmdParams = new List<SQLiteParameter>();

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    string term = $"%{filter.SearchTerm.Trim()}%";
                    sbWhere.Append(" AND (p.name_en LIKE @term OR p.name_ta LIKE @term OR p.barcode LIKE @term OR p.product_code LIKE @term) ");
                    cmdParams.Add(new SQLiteParameter("@term", term));
                }

                if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
                {
                    sbWhere.Append(" AND p.category_id = @catId ");
                    cmdParams.Add(new SQLiteParameter("@catId", filter.CategoryId.Value));
                }

                if (filter.BrandId.HasValue && filter.BrandId.Value > 0)
                {
                    sbWhere.Append(" AND p.brand_id = @brandId ");
                    cmdParams.Add(new SQLiteParameter("@brandId", filter.BrandId.Value));
                }

                if (!string.IsNullOrWhiteSpace(filter.StockStatus) && filter.StockStatus != "All")
                {
                    switch (filter.StockStatus)
                    {
                        case "In Stock":
                            sbWhere.Append(" AND p.current_stock > p.reorder_level ");
                            break;
                        case "Low Stock":
                            sbWhere.Append(" AND p.current_stock > 0 AND p.current_stock <= p.reorder_level ");
                            break;
                        case "Out of Stock":
                            sbWhere.Append(" AND p.current_stock <= 0 ");
                            break;
                        case "Excess Stock":
                            sbWhere.Append(" AND p.max_stock_level > 0 AND p.current_stock > p.max_stock_level ");
                            break;
                    }
                }

                // Count query
                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM products p {sbWhere};";
                    foreach (var p in cmdParams) countCmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                }

                // Data query with purchased qty, free qty, adjustment calculations
                string orderCol = filter.SortBy == "stockValue" ? "(p.current_stock * p.purchase_rate)" :
                                 filter.SortBy == "currentStock" ? "p.current_stock" :
                                 filter.SortBy == "barcode" ? "p.barcode" : "p.name_en";
                string orderDir = filter.SortDescending ? "DESC" : "ASC";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT
                            p.id AS product_id,
                            p.product_code,
                            p.barcode,
                            p.name_en AS product_name,
                            c.name AS category_name,
                            b.name AS brand_name,
                            p.unit,
                            p.opening_stock,
                            p.current_stock,
                            p.reorder_level,
                            p.max_stock_level,
                            p.purchase_rate,
                            p.sale_rate,
                            COALESCE((
                                SELECT SUM(pi.quantity)
                                FROM purchase_items pi
                                JOIN purchases pur ON pi.purchase_id = pur.id
                                WHERE pi.product_id = p.id AND pur.status = 'Confirmed'
                            ), 0) AS purchased_qty,
                            COALESCE((
                                SELECT SUM(pi.free_quantity)
                                FROM purchase_items pi
                                JOIN purchases pur ON pi.purchase_id = pur.id
                                WHERE pi.product_id = p.id AND pur.status = 'Confirmed'
                            ), 0) AS free_qty,
                            COALESCE((
                                SELECT SUM(sai.difference_quantity)
                                FROM stock_adjustment_items sai
                                JOIN stock_adjustments sa ON sai.adjustment_id = sa.id
                                WHERE sai.product_id = p.id AND sa.status = 'Confirmed'
                            ), 0) AS adjustment_qty
                        FROM products p
                        LEFT JOIN categories c ON p.category_id = c.id
                        LEFT JOIN brands b ON p.brand_id = b.id
                        {sbWhere}
                        ORDER BY {orderCol} {orderDir}
                        LIMIT @limit OFFSET @offset;
                    ";

                    foreach (var p in cmdParams) cmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    cmd.Parameters.AddWithValue("@limit", filter.PageSize);
                    cmd.Parameters.AddWithValue("@offset", (filter.PageNumber - 1) * filter.PageSize);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            items.Add(new CurrentStockSummary
                            {
                                ProductId = Convert.ToInt64(r["product_id"]),
                                ProductCode = r["product_code"].ToString(),
                                Barcode = r["barcode"].ToString(),
                                ProductName = r["product_name"].ToString(),
                                CategoryName = r["category_name"] == DBNull.Value ? "—" : r["category_name"].ToString(),
                                BrandName = r["brand_name"] == DBNull.Value ? "—" : r["brand_name"].ToString(),
                                Unit = r["unit"].ToString(),
                                OpeningStock = Convert.ToDecimal(r["opening_stock"]),
                                CurrentStock = Convert.ToDecimal(r["current_stock"]),
                                ReorderLevel = Convert.ToDecimal(r["reorder_level"]),
                                MaxStockLevel = Convert.ToDecimal(r["max_stock_level"]),
                                PurchaseRate = Convert.ToDecimal(r["purchase_rate"]),
                                SaleRate = Convert.ToDecimal(r["sale_rate"]),
                                PurchasedQuantity = Convert.ToDecimal(r["purchased_qty"]),
                                FreeQuantityReceived = Convert.ToDecimal(r["free_qty"]),
                                AdjustmentQuantity = Convert.ToDecimal(r["adjustment_qty"]),
                                SoldQuantity = 0m,     // 0 until Phase 4 POS Billing
                                ReturnedQuantity = 0m  // 0 until returns module
                            });
                        }
                    }
                }
            }

            sw.Stop();
            int totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            return new PagedResult<CurrentStockSummary>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        public IEnumerable<StockValuationSummary> GetStockValuation(long? categoryId = null)
        {
            var list = new List<StockValuationSummary>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                string sql = @"
                    SELECT
                        p.id AS product_id,
                        p.product_code,
                        p.barcode,
                        p.name_en AS product_name,
                        c.name AS category_name,
                        p.current_stock,
                        p.purchase_rate,
                        p.sale_rate
                    FROM products p
                    LEFT JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1 AND p.current_stock > 0
                ";
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    sql += " AND p.category_id = @catId ";
                    cmd.Parameters.AddWithValue("@catId", categoryId.Value);
                }
                sql += " ORDER BY (p.current_stock * p.purchase_rate) DESC;";

                cmd.CommandText = sql;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new StockValuationSummary
                        {
                            ProductId = Convert.ToInt64(r["product_id"]),
                            ProductCode = r["product_code"].ToString(),
                            Barcode = r["barcode"].ToString(),
                            ProductName = r["product_name"].ToString(),
                            CategoryName = r["category_name"] == DBNull.Value ? "Uncategorized" : r["category_name"].ToString(),
                            CurrentQuantity = Convert.ToDecimal(r["current_stock"]),
                            CostRate = Convert.ToDecimal(r["purchase_rate"]),
                            SellingPrice = Convert.ToDecimal(r["sale_rate"]),
                            ValuationMethod = "Purchase Cost (FIFO Equivalent)"
                        });
                    }
                }
            }
            return list;
        }

        public PagedResult<StockMovement> GetMovements(StockMovementFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var list = new List<StockMovement>();
            int totalCount = 0;

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var sbWhere = new StringBuilder(" WHERE 1=1 ");
                var cmdParams = new List<SQLiteParameter>();

                if (filter.ProductId.HasValue && filter.ProductId.Value > 0)
                {
                    sbWhere.Append(" AND sm.product_id = @prodId ");
                    cmdParams.Add(new SQLiteParameter("@prodId", filter.ProductId.Value));
                }

                if (!string.IsNullOrWhiteSpace(filter.MovementType) && filter.MovementType != "All")
                {
                    sbWhere.Append(" AND sm.reference_type = @mType ");
                    cmdParams.Add(new SQLiteParameter("@mType", filter.MovementType));
                }

                if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
                {
                    sbWhere.Append(" AND sm.reference_id LIKE @refNum ");
                    cmdParams.Add(new SQLiteParameter("@refNum", $"%{filter.ReferenceNumber.Trim()}%"));
                }

                if (filter.FromDate.HasValue)
                {
                    sbWhere.Append(" AND sm.created_at >= @fromDate ");
                    cmdParams.Add(new SQLiteParameter("@fromDate", filter.FromDate.Value));
                }

                if (filter.ToDate.HasValue)
                {
                    sbWhere.Append(" AND sm.created_at <= @toDate ");
                    cmdParams.Add(new SQLiteParameter("@toDate", filter.ToDate.Value));
                }

                // Count
                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM stock_movements sm {sbWhere};";
                    foreach (var p in cmdParams) countCmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                }

                // Records
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT sm.*, p.product_code, p.name_en AS product_name
                        FROM stock_movements sm
                        JOIN products p ON sm.product_id = p.id
                        {sbWhere}
                        ORDER BY sm.created_at DESC, sm.id DESC
                        LIMIT @limit OFFSET @offset;
                    ";
                    foreach (var p in cmdParams) cmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    cmd.Parameters.AddWithValue("@limit", filter.PageSize);
                    cmd.Parameters.AddWithValue("@offset", (filter.PageNumber - 1) * filter.PageSize);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new StockMovement
                            {
                                Id = Convert.ToInt64(r["id"]),
                                ProductId = Convert.ToInt64(r["product_id"]),
                                ProductCode = r["product_code"].ToString(),
                                ProductName = r["product_name"].ToString(),
                                ReferenceType = r["reference_type"].ToString(),
                                ReferenceId = r["reference_id"].ToString(),
                                Quantity = Convert.ToDecimal(r["quantity"]),
                                QuantityBefore = Convert.ToDecimal(r["quantity_before"]),
                                QuantityAfter = Convert.ToDecimal(r["quantity_after"]),
                                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                                CreatedAt = Convert.ToDateTime(r["created_at"])
                            });
                        }
                    }
                }
            }

            sw.Stop();
            int totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            return new PagedResult<StockMovement>
            {
                Items = list,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        public string GenerateNextAdjustmentNumber()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                int year = DateTime.Now.Year;
                string prefix = $"ADJ-{year}-";
                cmd.CommandText = "SELECT COUNT(*) FROM stock_adjustments WHERE adjustment_number LIKE @pfx;";
                cmd.Parameters.AddWithValue("@pfx", prefix + "%");
                int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                return $"{prefix}{(count + 1).ToString().PadLeft(4, '0')}";
            }
        }

        public string GenerateNextVerificationNumber()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                int year = DateTime.Now.Year;
                string prefix = $"VER-{year}-";
                cmd.CommandText = "SELECT COUNT(*) FROM physical_verifications WHERE verification_number LIKE @pfx;";
                cmd.Parameters.AddWithValue("@pfx", prefix + "%");
                int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                return $"{prefix}{(count + 1).ToString().PadLeft(4, '0')}";
            }
        }

        public long CreateAdjustmentDraft(StockAdjustment adj)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    long adjId;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO stock_adjustments (
                                adjustment_number, adjustment_date, reason, notes, status,
                                total_items, total_difference_quantity, total_cost_impact,
                                created_by, created_at, updated_at
                            ) VALUES (
                                @num, @date, @reason, @notes, 'Draft',
                                @items, @diffQty, @costImpact,
                                @by, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                            );
                            SELECT last_insert_rowid();
                        ";
                        cmd.Parameters.AddWithValue("@num", adj.AdjustmentNumber);
                        cmd.Parameters.AddWithValue("@date", adj.AdjustmentDate);
                        cmd.Parameters.AddWithValue("@reason", adj.Reason ?? "Manual count correction");
                        cmd.Parameters.AddWithValue("@notes", (object)adj.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@items", adj.Items.Count);
                        cmd.Parameters.AddWithValue("@diffQty", adj.Items.Sum(i => i.DifferenceQuantity));
                        cmd.Parameters.AddWithValue("@costImpact", adj.Items.Sum(i => i.CostImpact));
                        cmd.Parameters.AddWithValue("@by", adj.CreatedBy ?? "Admin");
                        adjId = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    foreach (var item in adj.Items)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
                                INSERT INTO stock_adjustment_items (
                                    adjustment_id, product_id, current_stock, physical_count,
                                    difference_quantity, adjustment_type, unit_cost, reason, notes
                                ) VALUES (
                                    @adjId, @pId, @curr, @phys,
                                    @diff, @type, @cost, @itemReason, @itemNotes
                                );
                            ";
                            cmd.Parameters.AddWithValue("@adjId", adjId);
                            cmd.Parameters.AddWithValue("@pId", item.ProductId);
                            cmd.Parameters.AddWithValue("@curr", item.CurrentStock);
                            cmd.Parameters.AddWithValue("@phys", item.PhysicalCount);
                            cmd.Parameters.AddWithValue("@diff", item.DifferenceQuantity);
                            cmd.Parameters.AddWithValue("@type", item.AdjustmentType ?? "Physical Count Correction");
                            cmd.Parameters.AddWithValue("@cost", item.UnitCost);
                            cmd.Parameters.AddWithValue("@itemReason", (object)item.Reason ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@itemNotes", (object)item.Notes ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    return adjId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public bool UpdateAdjustmentDraft(StockAdjustment adj)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            UPDATE stock_adjustments SET
                                reason = @reason,
                                notes = @notes,
                                total_items = @items,
                                total_difference_quantity = @diffQty,
                                total_cost_impact = @costImpact,
                                updated_at = CURRENT_TIMESTAMP
                            WHERE id = @id AND status = 'Draft';
                        ";
                        cmd.Parameters.AddWithValue("@id", adj.Id);
                        cmd.Parameters.AddWithValue("@reason", adj.Reason);
                        cmd.Parameters.AddWithValue("@notes", (object)adj.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@items", adj.Items.Count);
                        cmd.Parameters.AddWithValue("@diffQty", adj.Items.Sum(i => i.DifferenceQuantity));
                        cmd.Parameters.AddWithValue("@costImpact", adj.Items.Sum(i => i.CostImpact));
                        cmd.ExecuteNonQuery();
                    }

                    // Delete existing items and re-insert
                    using (var delCmd = conn.CreateCommand())
                    {
                        delCmd.Transaction = tx;
                        delCmd.CommandText = "DELETE FROM stock_adjustment_items WHERE adjustment_id = @adjId;";
                        delCmd.Parameters.AddWithValue("@adjId", adj.Id);
                        delCmd.ExecuteNonQuery();
                    }

                    foreach (var item in adj.Items)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
                                INSERT INTO stock_adjustment_items (
                                    adjustment_id, product_id, current_stock, physical_count,
                                    difference_quantity, adjustment_type, unit_cost, reason, notes
                                ) VALUES (
                                    @adjId, @pId, @curr, @phys,
                                    @diff, @type, @cost, @itemReason, @itemNotes
                                );
                            ";
                            cmd.Parameters.AddWithValue("@adjId", adj.Id);
                            cmd.Parameters.AddWithValue("@pId", item.ProductId);
                            cmd.Parameters.AddWithValue("@curr", item.CurrentStock);
                            cmd.Parameters.AddWithValue("@phys", item.PhysicalCount);
                            cmd.Parameters.AddWithValue("@diff", item.DifferenceQuantity);
                            cmd.Parameters.AddWithValue("@type", item.AdjustmentType);
                            cmd.Parameters.AddWithValue("@cost", item.UnitCost);
                            cmd.Parameters.AddWithValue("@itemReason", (object)item.Reason ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@itemNotes", (object)item.Notes ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public bool ConfirmAdjustment(long adjustmentId, out string errorMessage)
        {
            errorMessage = null;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // 1. Fetch adjustment header and check status
                    string adjNumber;
                    string status;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.Transaction = tx;
                        checkCmd.CommandText = "SELECT adjustment_number, status FROM stock_adjustments WHERE id = @id;";
                        checkCmd.Parameters.AddWithValue("@id", adjustmentId);
                        using (var r = checkCmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                errorMessage = "Stock adjustment not found.";
                                return false;
                            }
                            adjNumber = r["adjustment_number"].ToString();
                            status = r["status"].ToString();
                        }
                    }

                    if (status == "Confirmed")
                    {
                        errorMessage = "Stock adjustment is already confirmed. Duplicate confirmation prevented.";
                        return false;
                    }
                    if (status == "Cancelled")
                    {
                        errorMessage = "Cannot confirm a cancelled stock adjustment.";
                        return false;
                    }

                    // 2. Fetch items
                    var items = new List<StockAdjustmentItem>();
                    using (var itemCmd = conn.CreateCommand())
                    {
                        itemCmd.Transaction = tx;
                        itemCmd.CommandText = @"
                            SELECT sai.*, p.name_en AS product_name, p.current_stock AS latest_stock
                            FROM stock_adjustment_items sai
                            JOIN products p ON sai.product_id = p.id
                            WHERE sai.adjustment_id = @adjId;
                        ";
                        itemCmd.Parameters.AddWithValue("@adjId", adjustmentId);
                        using (var r = itemCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                items.Add(new StockAdjustmentItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    AdjustmentId = adjustmentId,
                                    ProductId = Convert.ToInt64(r["product_id"]),
                                    ProductName = r["product_name"].ToString(),
                                    CurrentStock = Convert.ToDecimal(r["latest_stock"]),
                                    PhysicalCount = Convert.ToDecimal(r["physical_count"]),
                                    DifferenceQuantity = Convert.ToDecimal(r["difference_quantity"]),
                                    AdjustmentType = r["adjustment_type"].ToString(),
                                    Reason = r["reason"] == DBNull.Value ? null : r["reason"].ToString()
                                });
                            }
                        }
                    }

                    if (items.Count == 0)
                    {
                        errorMessage = "Adjustment has no items.";
                        return false;
                    }

                    // 3. Atomically validate and update products and write stock movements
                    foreach (var it in items)
                    {
                        decimal beforeStock = it.CurrentStock;
                        decimal delta = it.DifferenceQuantity;
                        decimal afterStock = beforeStock + delta;

                        // Negative stock guard
                        if (afterStock < 0)
                        {
                            errorMessage = $"Adjustment would result in negative stock ({afterStock}) for product '{it.ProductName}'. Operation aborted.";
                            tx.Rollback();
                            return false;
                        }

                        // Update product current stock
                        using (var updateCmd = conn.CreateCommand())
                        {
                            updateCmd.Transaction = tx;
                            updateCmd.CommandText = @"
                                UPDATE products
                                SET current_stock = @after, updated_at = CURRENT_TIMESTAMP
                                WHERE id = @pId;
                            ";
                            updateCmd.Parameters.AddWithValue("@after", afterStock);
                            updateCmd.Parameters.AddWithValue("@pId", it.ProductId);
                            updateCmd.ExecuteNonQuery();
                        }

                        // Write to stock_movements ledger
                        using (var movCmd = conn.CreateCommand())
                        {
                            movCmd.Transaction = tx;
                            movCmd.CommandText = @"
                                INSERT INTO stock_movements (
                                    product_id, reference_type, reference_id,
                                    quantity, quantity_before, quantity_after,
                                    notes, created_at
                                ) VALUES (
                                    @pId, @refType, @refId,
                                    @qty, @before, @after,
                                    @notes, CURRENT_TIMESTAMP
                                );
                            ";
                            string movRefType = delta >= 0 ? "STOCK_ADJUSTMENT_IN" : "STOCK_ADJUSTMENT_OUT";
                            string note = $"{it.AdjustmentType}: {it.Reason ?? "Manual Count Correction"} (Difference: {delta:+#0.##;-#0.##;0})";

                            movCmd.Parameters.AddWithValue("@pId", it.ProductId);
                            movCmd.Parameters.AddWithValue("@refType", movRefType);
                            movCmd.Parameters.AddWithValue("@refId", adjNumber);
                            movCmd.Parameters.AddWithValue("@qty", delta);
                            movCmd.Parameters.AddWithValue("@before", beforeStock);
                            movCmd.Parameters.AddWithValue("@after", afterStock);
                            movCmd.Parameters.AddWithValue("@notes", note);
                            movCmd.ExecuteNonQuery();
                        }
                    }

                    // 4. Update adjustment header
                    using (var confCmd = conn.CreateCommand())
                    {
                        confCmd.Transaction = tx;
                        confCmd.CommandText = @"
                            UPDATE stock_adjustments
                            SET status = 'Confirmed', confirmed_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP
                            WHERE id = @id;
                        ";
                        confCmd.Parameters.AddWithValue("@id", adjustmentId);
                        confCmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    errorMessage = ex.Message;
                    return false;
                }
            }
        }

        public bool CancelAdjustment(long adjustmentId, out string errorMessage)
        {
            errorMessage = null;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    string adjNumber;
                    string status;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.Transaction = tx;
                        checkCmd.CommandText = "SELECT adjustment_number, status FROM stock_adjustments WHERE id = @id;";
                        checkCmd.Parameters.AddWithValue("@id", adjustmentId);
                        using (var r = checkCmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                errorMessage = "Stock adjustment not found.";
                                return false;
                            }
                            adjNumber = r["adjustment_number"].ToString();
                            status = r["status"].ToString();
                        }
                    }

                    if (status != "Confirmed")
                    {
                        errorMessage = "Only confirmed adjustments can be cancelled/reverted.";
                        return false;
                    }

                    // Read items
                    var items = new List<StockAdjustmentItem>();
                    using (var itemCmd = conn.CreateCommand())
                    {
                        itemCmd.Transaction = tx;
                        itemCmd.CommandText = @"
                            SELECT sai.*, p.name_en AS product_name, p.current_stock AS latest_stock
                            FROM stock_adjustment_items sai
                            JOIN products p ON sai.product_id = p.id
                            WHERE sai.adjustment_id = @adjId;
                        ";
                        itemCmd.Parameters.AddWithValue("@adjId", adjustmentId);
                        using (var r = itemCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                items.Add(new StockAdjustmentItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    AdjustmentId = adjustmentId,
                                    ProductId = Convert.ToInt64(r["product_id"]),
                                    ProductName = r["product_name"].ToString(),
                                    CurrentStock = Convert.ToDecimal(r["latest_stock"]),
                                    DifferenceQuantity = Convert.ToDecimal(r["difference_quantity"])
                                });
                            }
                        }
                    }

                    // Reverse delta
                    foreach (var it in items)
                    {
                        decimal beforeStock = it.CurrentStock;
                        decimal reverseDelta = -it.DifferenceQuantity; // Invert delta
                        decimal afterStock = beforeStock + reverseDelta;

                        if (afterStock < 0)
                        {
                            errorMessage = $"Reverting adjustment would cause negative stock ({afterStock}) for '{it.ProductName}'. Operation rejected.";
                            tx.Rollback();
                            return false;
                        }

                        using (var updCmd = conn.CreateCommand())
                        {
                            updCmd.Transaction = tx;
                            updCmd.CommandText = "UPDATE products SET current_stock = @after, updated_at = CURRENT_TIMESTAMP WHERE id = @pId;";
                            updCmd.Parameters.AddWithValue("@after", afterStock);
                            updCmd.Parameters.AddWithValue("@pId", it.ProductId);
                            updCmd.ExecuteNonQuery();
                        }

                        // Write reversal ledger movement
                        using (var movCmd = conn.CreateCommand())
                        {
                            movCmd.Transaction = tx;
                            movCmd.CommandText = @"
                                INSERT INTO stock_movements (
                                    product_id, reference_type, reference_id,
                                    quantity, quantity_before, quantity_after,
                                    notes, created_at
                                ) VALUES (
                                    @pId, 'ADJUSTMENT_CANCEL', @refId,
                                    @qty, @before, @after,
                                    @notes, CURRENT_TIMESTAMP
                                );
                            ";
                            movCmd.Parameters.AddWithValue("@pId", it.ProductId);
                            movCmd.Parameters.AddWithValue("@refId", adjNumber);
                            movCmd.Parameters.AddWithValue("@qty", reverseDelta);
                            movCmd.Parameters.AddWithValue("@before", beforeStock);
                            movCmd.Parameters.AddWithValue("@after", afterStock);
                            movCmd.Parameters.AddWithValue("@notes", $"Reversal of cancelled adjustment {adjNumber}");
                            movCmd.ExecuteNonQuery();
                        }
                    }

                    using (var updAdj = conn.CreateCommand())
                    {
                        updAdj.Transaction = tx;
                        updAdj.CommandText = "UPDATE stock_adjustments SET status = 'Cancelled', cancelled_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                        updAdj.Parameters.AddWithValue("@id", adjustmentId);
                        updAdj.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    errorMessage = ex.Message;
                    return false;
                }
            }
        }

        public StockAdjustment GetAdjustmentById(long id)
        {
            StockAdjustment adj = null;
            using (var conn = DatabaseConnection.CreateConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM stock_adjustments WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            adj = new StockAdjustment
                            {
                                Id = Convert.ToInt64(r["id"]),
                                AdjustmentNumber = r["adjustment_number"].ToString(),
                                AdjustmentDate = Convert.ToDateTime(r["adjustment_date"]),
                                Reason = r["reason"].ToString(),
                                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                                Status = r["status"].ToString(),
                                TotalItems = Convert.ToInt32(r["total_items"]),
                                TotalDifferenceQuantity = Convert.ToDecimal(r["total_difference_quantity"]),
                                TotalCostImpact = Convert.ToDecimal(r["total_cost_impact"]),
                                CreatedBy = r["created_by"].ToString(),
                                ConfirmedAt = r["confirmed_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["confirmed_at"]),
                                CancelledAt = r["cancelled_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["cancelled_at"]),
                                CreatedAt = Convert.ToDateTime(r["created_at"]),
                                UpdatedAt = Convert.ToDateTime(r["updated_at"])
                            };
                        }
                    }
                }

                if (adj != null)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT sai.*, p.product_code, p.barcode, p.name_en AS product_name, c.name AS category_name
                            FROM stock_adjustment_items sai
                            JOIN products p ON sai.product_id = p.id
                            LEFT JOIN categories c ON p.category_id = c.id
                            WHERE sai.adjustment_id = @id;
                        ";
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                adj.Items.Add(new StockAdjustmentItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    AdjustmentId = id,
                                    ProductId = Convert.ToInt64(r["product_id"]),
                                    ProductCode = r["product_code"].ToString(),
                                    Barcode = r["barcode"].ToString(),
                                    ProductName = r["product_name"].ToString(),
                                    CategoryName = r["category_name"] == DBNull.Value ? "—" : r["category_name"].ToString(),
                                    CurrentStock = Convert.ToDecimal(r["current_stock"]),
                                    PhysicalCount = Convert.ToDecimal(r["physical_count"]),
                                    DifferenceQuantity = Convert.ToDecimal(r["difference_quantity"]),
                                    AdjustmentType = r["adjustment_type"].ToString(),
                                    UnitCost = Convert.ToDecimal(r["unit_cost"]),
                                    Reason = r["reason"] == DBNull.Value ? null : r["reason"].ToString(),
                                    Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            return adj;
        }

        public PagedResult<StockAdjustment> SearchAdjustments(StockAdjustmentFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var list = new List<StockAdjustment>();
            int totalCount = 0;

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var sbWhere = new StringBuilder(" WHERE 1=1 ");
                var cmdParams = new List<SQLiteParameter>();

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    sbWhere.Append(" AND (adjustment_number LIKE @term OR reason LIKE @term OR notes LIKE @term) ");
                    cmdParams.Add(new SQLiteParameter("@term", $"%{filter.SearchTerm.Trim()}%"));
                }

                if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "All")
                {
                    sbWhere.Append(" AND status = @status ");
                    cmdParams.Add(new SQLiteParameter("@status", filter.Status));
                }

                if (filter.FromDate.HasValue)
                {
                    sbWhere.Append(" AND adjustment_date >= @fromDate ");
                    cmdParams.Add(new SQLiteParameter("@fromDate", filter.FromDate.Value));
                }

                if (filter.ToDate.HasValue)
                {
                    sbWhere.Append(" AND adjustment_date <= @toDate ");
                    cmdParams.Add(new SQLiteParameter("@toDate", filter.ToDate.Value));
                }

                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM stock_adjustments {sbWhere};";
                    foreach (var p in cmdParams) countCmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT * FROM stock_adjustments
                        {sbWhere}
                        ORDER BY adjustment_date DESC, id DESC
                        LIMIT @limit OFFSET @offset;
                    ";
                    foreach (var p in cmdParams) cmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    cmd.Parameters.AddWithValue("@limit", filter.PageSize);
                    cmd.Parameters.AddWithValue("@offset", (filter.PageNumber - 1) * filter.PageSize);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new StockAdjustment
                            {
                                Id = Convert.ToInt64(r["id"]),
                                AdjustmentNumber = r["adjustment_number"].ToString(),
                                AdjustmentDate = Convert.ToDateTime(r["adjustment_date"]),
                                Reason = r["reason"].ToString(),
                                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                                Status = r["status"].ToString(),
                                TotalItems = Convert.ToInt32(r["total_items"]),
                                TotalDifferenceQuantity = Convert.ToDecimal(r["total_difference_quantity"]),
                                TotalCostImpact = Convert.ToDecimal(r["total_cost_impact"]),
                                CreatedBy = r["created_by"].ToString(),
                                ConfirmedAt = r["confirmed_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["confirmed_at"]),
                                CancelledAt = r["cancelled_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["cancelled_at"]),
                                CreatedAt = Convert.ToDateTime(r["created_at"]),
                                UpdatedAt = Convert.ToDateTime(r["updated_at"])
                            });
                        }
                    }
                }
            }

            sw.Stop();
            int totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            return new PagedResult<StockAdjustment>
            {
                Items = list,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        public long CreatePhysicalVerificationDraft(PhysicalStockVerification ver)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    long verId;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO physical_verifications (
                                verification_number, verification_date, category_id, status,
                                total_counted_products, total_discrepancies, net_difference_quantity,
                                notes, created_by, created_at, updated_at
                            ) VALUES (
                                @num, @date, @catId, 'Draft',
                                @counted, @disc, @netDiff,
                                @notes, @by, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                            );
                            SELECT last_insert_rowid();
                        ";
                        cmd.Parameters.AddWithValue("@num", ver.VerificationNumber);
                        cmd.Parameters.AddWithValue("@date", ver.VerificationDate);
                        cmd.Parameters.AddWithValue("@catId", (object)ver.CategoryId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@counted", ver.Items.Count);
                        cmd.Parameters.AddWithValue("@disc", ver.Items.Count(i => i.DifferenceQuantity != 0));
                        cmd.Parameters.AddWithValue("@netDiff", ver.Items.Sum(i => i.DifferenceQuantity));
                        cmd.Parameters.AddWithValue("@notes", (object)ver.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@by", ver.CreatedBy ?? "Admin");
                        verId = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    foreach (var it in ver.Items)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
                                INSERT INTO physical_verification_items (
                                    verification_id, product_id, system_stock_at_start,
                                    physical_count, difference_quantity, discrepancy_resolved, notes
                                ) VALUES (
                                    @verId, @pId, @startStock,
                                    @phys, @diff, @resolved, @notes
                                );
                            ";
                            cmd.Parameters.AddWithValue("@verId", verId);
                            cmd.Parameters.AddWithValue("@pId", it.ProductId);
                            cmd.Parameters.AddWithValue("@startStock", it.SystemStockAtStart);
                            cmd.Parameters.AddWithValue("@phys", it.PhysicalCount);
                            cmd.Parameters.AddWithValue("@diff", it.DifferenceQuantity);
                            cmd.Parameters.AddWithValue("@resolved", it.DiscrepancyResolved ? 1 : 0);
                            cmd.Parameters.AddWithValue("@notes", (object)it.Notes ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    return verId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public bool ConfirmPhysicalVerification(long verificationId, out string errorMessage)
        {
            errorMessage = null;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // 1. Fetch verification header
                    string verNum;
                    string status;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.Transaction = tx;
                        checkCmd.CommandText = "SELECT verification_number, status FROM physical_verifications WHERE id = @id;";
                        checkCmd.Parameters.AddWithValue("@id", verificationId);
                        using (var r = checkCmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                errorMessage = "Physical verification record not found.";
                                return false;
                            }
                            verNum = r["verification_number"].ToString();
                            status = r["status"].ToString();
                        }
                    }

                    if (status == "Confirmed")
                    {
                        errorMessage = "Physical verification count has already been confirmed.";
                        return false;
                    }

                    // 2. Fetch items and check concurrent changes!
                    var items = new List<PhysicalStockVerificationItem>();
                    using (var itemCmd = conn.CreateCommand())
                    {
                        itemCmd.Transaction = tx;
                        itemCmd.CommandText = @"
                            SELECT pvi.*, p.name_en AS product_name, p.current_stock AS latest_stock
                            FROM physical_verification_items pvi
                            JOIN products p ON pvi.product_id = p.id
                            WHERE pvi.verification_id = @verId;
                        ";
                        itemCmd.Parameters.AddWithValue("@verId", verificationId);
                        using (var r = itemCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                items.Add(new PhysicalStockVerificationItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    VerificationId = verificationId,
                                    ProductId = Convert.ToInt64(r["product_id"]),
                                    ProductName = r["product_name"].ToString(),
                                    SystemStockAtStart = Convert.ToDecimal(r["system_stock_at_start"]),
                                    SystemStockAtConfirm = Convert.ToDecimal(r["latest_stock"]),
                                    PhysicalCount = Convert.ToDecimal(r["physical_count"]),
                                    DifferenceQuantity = Convert.ToDecimal(r["difference_quantity"])
                                });
                            }
                        }
                    }

                    // Check for discrepancies and update
                    int reconciledCount = 0;
                    foreach (var it in items)
                    {
                        // Discrepancy recalculated against latest stock
                        decimal liveDifference = it.PhysicalCount - it.SystemStockAtConfirm;
                        if (liveDifference != 0)
                        {
                            decimal before = it.SystemStockAtConfirm;
                            decimal after = it.PhysicalCount;

                            if (after < 0)
                            {
                                errorMessage = $"Physical reconciliation for '{it.ProductName}' results in negative stock ({after}). Aborted.";
                                tx.Rollback();
                                return false;
                            }

                            // Update product current stock to physical count
                            using (var pUpd = conn.CreateCommand())
                            {
                                pUpd.Transaction = tx;
                                pUpd.CommandText = "UPDATE products SET current_stock = @after, updated_at = CURRENT_TIMESTAMP WHERE id = @pId;";
                                pUpd.Parameters.AddWithValue("@after", after);
                                pUpd.Parameters.AddWithValue("@pId", it.ProductId);
                                pUpd.ExecuteNonQuery();
                            }

                            // Record movement in stock_movements
                            using (var movCmd = conn.CreateCommand())
                            {
                                movCmd.Transaction = tx;
                                movCmd.CommandText = @"
                                    INSERT INTO stock_movements (
                                        product_id, reference_type, reference_id,
                                        quantity, quantity_before, quantity_after,
                                        notes, created_at
                                    ) VALUES (
                                        @pId, 'PHYSICAL_VERIFICATION', @refId,
                                        @qty, @before, @after,
                                        @notes, CURRENT_TIMESTAMP
                                    );
                                ";
                                movCmd.Parameters.AddWithValue("@pId", it.ProductId);
                                movCmd.Parameters.AddWithValue("@refId", verNum);
                                movCmd.Parameters.AddWithValue("@qty", liveDifference);
                                movCmd.Parameters.AddWithValue("@before", before);
                                movCmd.Parameters.AddWithValue("@after", after);
                                movCmd.Parameters.AddWithValue("@notes", $"Physical Stock Audit Reconciled (Physical: {it.PhysicalCount}, System was: {before})");
                                movCmd.ExecuteNonQuery();
                            }

                            reconciledCount++;
                        }
                    }

                    // Update verification header
                    using (var confCmd = conn.CreateCommand())
                    {
                        confCmd.Transaction = tx;
                        confCmd.CommandText = @"
                            UPDATE physical_verifications
                            SET status = 'Confirmed', confirmed_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP
                            WHERE id = @id;
                        ";
                        confCmd.Parameters.AddWithValue("@id", verificationId);
                        confCmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    errorMessage = ex.Message;
                    return false;
                }
            }
        }

        public PhysicalStockVerification GetPhysicalVerificationById(long id)
        {
            PhysicalStockVerification ver = null;
            using (var conn = DatabaseConnection.CreateConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT pv.*, c.name AS category_name
                        FROM physical_verifications pv
                        LEFT JOIN categories c ON pv.category_id = c.id
                        WHERE pv.id = @id;
                    ";
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            ver = new PhysicalStockVerification
                            {
                                Id = Convert.ToInt64(r["id"]),
                                VerificationNumber = r["verification_number"].ToString(),
                                VerificationDate = Convert.ToDateTime(r["verification_date"]),
                                CategoryId = r["category_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["category_id"]),
                                CategoryName = r["category_name"] == DBNull.Value ? "All Categories" : r["category_name"].ToString(),
                                Status = r["status"].ToString(),
                                TotalCountedProducts = Convert.ToInt32(r["total_counted_products"]),
                                TotalDiscrepancies = Convert.ToInt32(r["total_discrepancies"]),
                                NetDifferenceQuantity = Convert.ToDecimal(r["net_difference_quantity"]),
                                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                                CreatedBy = r["created_by"].ToString(),
                                ConfirmedAt = r["confirmed_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["confirmed_at"]),
                                CreatedAt = Convert.ToDateTime(r["created_at"]),
                                UpdatedAt = Convert.ToDateTime(r["updated_at"])
                            };
                        }
                    }
                }

                if (ver != null)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT pvi.*, p.product_code, p.barcode, p.name_en AS product_name, c.name AS category_name
                            FROM physical_verification_items pvi
                            JOIN products p ON pvi.product_id = p.id
                            LEFT JOIN categories c ON p.category_id = c.id
                            WHERE pvi.verification_id = @id;
                        ";
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                ver.Items.Add(new PhysicalStockVerificationItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    VerificationId = id,
                                    ProductId = Convert.ToInt64(r["product_id"]),
                                    ProductCode = r["product_code"].ToString(),
                                    Barcode = r["barcode"].ToString(),
                                    ProductName = r["product_name"].ToString(),
                                    CategoryName = r["category_name"] == DBNull.Value ? "—" : r["category_name"].ToString(),
                                    SystemStockAtStart = Convert.ToDecimal(r["system_stock_at_start"]),
                                    SystemStockAtConfirm = Convert.ToDecimal(r["system_stock_at_confirm"]),
                                    PhysicalCount = Convert.ToDecimal(r["physical_count"]),
                                    DifferenceQuantity = Convert.ToDecimal(r["difference_quantity"]),
                                    DiscrepancyResolved = Convert.ToInt32(r["discrepancy_resolved"]) == 1,
                                    Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            return ver;
        }
    }
}
