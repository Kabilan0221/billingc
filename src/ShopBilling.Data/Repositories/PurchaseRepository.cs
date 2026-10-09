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
    public class PurchaseRepository : IPurchaseRepository
    {
        public PurchaseHeader GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            {
                PurchaseHeader p = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT p.*, s.name AS supplier_name
                        FROM purchases p
                        JOIN suppliers s ON p.supplier_id = s.id
                        WHERE p.id = @id;
                    ";
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) p = MapPurchaseHeader(reader);
                    }
                }

                if (p != null)
                {
                    p.Items = GetPurchaseItems(conn, p.Id);
                }
                return p;
            }
        }

        public PurchaseHeader GetByNumber(string purchaseNumber)
        {
            if (string.IsNullOrWhiteSpace(purchaseNumber)) return null;

            using (var conn = DatabaseConnection.CreateConnection())
            {
                PurchaseHeader p = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT p.*, s.name AS supplier_name
                        FROM purchases p
                        JOIN suppliers s ON p.supplier_id = s.id
                        WHERE p.purchase_number = @num
                        LIMIT 1;
                    ";
                    cmd.Parameters.AddWithValue("@num", purchaseNumber.Trim());
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) p = MapPurchaseHeader(reader);
                    }
                }

                if (p != null)
                {
                    p.Items = GetPurchaseItems(conn, p.Id);
                }
                return p;
            }
        }

        public PagedResult<PurchaseHeader> Search(PurchaseFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var result = new PagedResult<PurchaseHeader>
            {
                PageNumber = Math.Max(1, filter.PageNumber),
                PageSize = Math.Max(1, filter.PageSize)
            };

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var where = new StringBuilder(" WHERE 1=1 ");
                var parameters = new List<SQLiteParameter>();

                if (filter.SupplierId.HasValue && filter.SupplierId.Value > 0)
                {
                    where.Append(" AND p.supplier_id = @supId ");
                    parameters.Add(new SQLiteParameter("@supId", filter.SupplierId.Value));
                }

                if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "All")
                {
                    where.Append(" AND p.status = @status ");
                    parameters.Add(new SQLiteParameter("@status", filter.Status));
                }

                if (filter.FromDate.HasValue)
                {
                    where.Append(" AND p.goods_received_date >= @fromDate ");
                    parameters.Add(new SQLiteParameter("@fromDate", filter.FromDate.Value.ToString("yyyy-MM-dd 00:00:00")));
                }

                if (filter.ToDate.HasValue)
                {
                    where.Append(" AND p.goods_received_date <= @toDate ");
                    parameters.Add(new SQLiteParameter("@toDate", filter.ToDate.Value.ToString("yyyy-MM-dd 23:59:59")));
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    var term = filter.SearchTerm.Trim();
                    where.Append(" AND (p.purchase_number LIKE @term OR p.supplier_invoice_number LIKE @term OR s.name LIKE @term) ");
                    parameters.Add(new SQLiteParameter("@term", $"%{term}%"));
                }

                // Total count
                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $@"
                        SELECT COUNT(*)
                        FROM purchases p
                        JOIN suppliers s ON p.supplier_id = s.id
                        {where};
                    ";
                    foreach (var param in parameters) countCmd.Parameters.Add(param);
                    result.TotalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                int offset = (result.PageNumber - 1) * result.PageSize;

                using (var queryCmd = conn.CreateCommand())
                {
                    queryCmd.CommandText = $@"
                        SELECT p.*, s.name AS supplier_name
                        FROM purchases p
                        JOIN suppliers s ON p.supplier_id = s.id
                        {where}
                        ORDER BY p.id DESC
                        LIMIT @limit OFFSET @offset;
                    ";
                    foreach (var param in parameters) queryCmd.Parameters.Add(new SQLiteParameter(param.ParameterName, param.Value));
                    queryCmd.Parameters.AddWithValue("@limit", result.PageSize);
                    queryCmd.Parameters.AddWithValue("@offset", offset);

                    using (var reader = queryCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Items.Add(MapPurchaseHeader(reader));
                        }
                    }
                }
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        public long CreateDraft(PurchaseHeader p)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    long purchaseId;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO purchases (
                                purchase_number, supplier_id, supplier_invoice_number, supplier_invoice_date,
                                goods_received_date, purchase_type, payment_due_date, reference_number, notes,
                                status, total_qty, total_free_qty, gross_amount, item_discount, additional_discount,
                                taxable_amount, tax_amount, additional_charges, round_off, grand_total, amount_paid,
                                balance_due, created_by, created_at, updated_at
                            ) VALUES (
                                @num, @supId, @invNum, @invDate,
                                @recDate, @type, @dueDate, @refNum, @notes,
                                'Draft', @tQty, @tFreeQty, @gross, @itemDisc, @addDisc,
                                @taxable, @tax, @addCharges, @roundOff, @grand, @paid,
                                @balDue, @createdBy, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                            );
                            SELECT last_insert_rowid();
                        ";
                        AddPurchaseHeaderParameters(cmd, p);
                        purchaseId = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    InsertLineItems(conn, tx, purchaseId, p.Items);
                    tx.Commit();
                    return purchaseId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public bool UpdateDraft(PurchaseHeader p)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // Check that purchase is still in Draft state
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.Transaction = tx;
                        checkCmd.CommandText = "SELECT status FROM purchases WHERE id = @id;";
                        checkCmd.Parameters.AddWithValue("@id", p.Id);
                        var status = checkCmd.ExecuteScalar()?.ToString();
                        if (status != "Draft")
                        {
                            throw new InvalidOperationException($"Only Draft purchases can be edited. Current status is '{status}'.");
                        }
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            UPDATE purchases SET
                                supplier_id = @supId,
                                supplier_invoice_number = @invNum,
                                supplier_invoice_date = @invDate,
                                goods_received_date = @recDate,
                                purchase_type = @type,
                                payment_due_date = @dueDate,
                                reference_number = @refNum,
                                notes = @notes,
                                total_qty = @tQty,
                                total_free_qty = @tFreeQty,
                                gross_amount = @gross,
                                item_discount = @itemDisc,
                                additional_discount = @addDisc,
                                taxable_amount = @taxable,
                                tax_amount = @tax,
                                additional_charges = @addCharges,
                                round_off = @roundOff,
                                grand_total = @grand,
                                amount_paid = @paid,
                                balance_due = @balDue,
                                updated_at = CURRENT_TIMESTAMP
                            WHERE id = @id;
                        ";
                        AddPurchaseHeaderParameters(cmd, p);
                        cmd.Parameters.AddWithValue("@id", p.Id);
                        cmd.ExecuteNonQuery();
                    }

                    // Replace items
                    using (var delCmd = conn.CreateCommand())
                    {
                        delCmd.Transaction = tx;
                        delCmd.CommandText = "DELETE FROM purchase_items WHERE purchase_id = @id;";
                        delCmd.Parameters.AddWithValue("@id", p.Id);
                        delCmd.ExecuteNonQuery();
                    }

                    InsertLineItems(conn, tx, p.Id, p.Items);
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

        public bool ConfirmPurchase(long purchaseId, out string errorMessage)
        {
            errorMessage = null;

            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // 1. Fetch current status & verify Draft state (Prevent duplicate confirmation!)
                    string currentStatus;
                    string purchaseNumber;
                    long supplierId;
                    decimal grandTotal;
                    decimal amountPaid;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "SELECT status, purchase_number, supplier_id, grand_total, amount_paid FROM purchases WHERE id = @id;";
                        cmd.Parameters.AddWithValue("@id", purchaseId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                errorMessage = "Purchase record not found.";
                                return false;
                            }
                            currentStatus = r["status"].ToString();
                            purchaseNumber = r["purchase_number"].ToString();
                            supplierId = Convert.ToInt64(r["supplier_id"]);
                            grandTotal = Convert.ToDecimal(r["grand_total"]);
                            amountPaid = Convert.ToDecimal(r["amount_paid"]);
                        }
                    }

                    if (currentStatus != "Draft")
                    {
                        errorMessage = $"Purchase '{purchaseNumber}' is already '{currentStatus}' and cannot be confirmed again.";
                        return false;
                    }

                    // 2. Fetch line items
                    var items = GetPurchaseItems(conn, purchaseId, tx);
                    if (items.Count == 0)
                    {
                        errorMessage = "Cannot confirm purchase with 0 line items.";
                        return false;
                    }

                    // 3. Update stock for each product atomically and record in stock_movements
                    foreach (var item in items)
                    {
                        decimal currentStock = 0;
                        using (var stockCmd = conn.CreateCommand())
                        {
                            stockCmd.Transaction = tx;
                            stockCmd.CommandText = "SELECT current_stock FROM products WHERE id = @prodId;";
                            stockCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            var scalar = stockCmd.ExecuteScalar();
                            if (scalar == null)
                            {
                                errorMessage = $"Product ID #{item.ProductId} ('{item.ProductName}') no longer exists.";
                                tx.Rollback();
                                return false;
                            }
                            currentStock = Convert.ToDecimal(scalar);
                        }

                        decimal totalReceived = item.Quantity + item.FreeQuantity;
                        decimal newStock = currentStock + totalReceived;

                        // Update product current stock
                        using (var updateStockCmd = conn.CreateCommand())
                        {
                            updateStockCmd.Transaction = tx;
                            updateStockCmd.CommandText = "UPDATE products SET current_stock = @newStock, updated_at = CURRENT_TIMESTAMP WHERE id = @prodId;";
                            updateStockCmd.Parameters.AddWithValue("@newStock", newStock);
                            updateStockCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            updateStockCmd.ExecuteNonQuery();
                        }

                        // Record in stock movements ledger
                        using (var moveCmd = conn.CreateCommand())
                        {
                            moveCmd.Transaction = tx;
                            moveCmd.CommandText = @"
                                INSERT INTO stock_movements (
                                    product_id, reference_type, reference_id, quantity,
                                    quantity_before, quantity_after, notes, created_at
                                ) VALUES (
                                    @prodId, 'PURCHASE_CONFIRM', @refId, @qty,
                                    @before, @after, @notes, CURRENT_TIMESTAMP
                                );
                            ";
                            moveCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            moveCmd.Parameters.AddWithValue("@refId", purchaseNumber);
                            moveCmd.Parameters.AddWithValue("@qty", totalReceived);
                            moveCmd.Parameters.AddWithValue("@before", currentStock);
                            moveCmd.Parameters.AddWithValue("@after", newStock);
                            moveCmd.Parameters.AddWithValue("@notes", $"Received via Purchase {purchaseNumber} (Purchased: {item.Quantity}, Free: {item.FreeQuantity})");
                            moveCmd.ExecuteNonQuery();
                        }
                    }

                    // 4. Update Supplier Balance & Ledger
                    decimal netPayableAddition = grandTotal - amountPaid;
                    decimal supplierBalBefore = 0;
                    using (var supCmd = conn.CreateCommand())
                    {
                        supCmd.Transaction = tx;
                        supCmd.CommandText = "SELECT current_balance FROM suppliers WHERE id = @supId;";
                        supCmd.Parameters.AddWithValue("@supId", supplierId);
                        supplierBalBefore = Convert.ToDecimal(supCmd.ExecuteScalar());
                    }

                    decimal supplierBalAfter = supplierBalBefore + netPayableAddition;

                    using (var updateSupCmd = conn.CreateCommand())
                    {
                        updateSupCmd.Transaction = tx;
                        updateSupCmd.CommandText = "UPDATE suppliers SET current_balance = @newBal, updated_at = CURRENT_TIMESTAMP WHERE id = @supId;";
                        updateSupCmd.Parameters.AddWithValue("@newBal", supplierBalAfter);
                        updateSupCmd.Parameters.AddWithValue("@supId", supplierId);
                        updateSupCmd.ExecuteNonQuery();
                    }

                    // Insert into supplier ledger
                    using (var ledgerCmd = conn.CreateCommand())
                    {
                        ledgerCmd.Transaction = tx;
                        ledgerCmd.CommandText = @"
                            INSERT INTO supplier_ledger (
                                supplier_id, transaction_type, reference_id, debit, credit, balance_after, notes, transaction_date
                            ) VALUES (
                                @supId, 'PURCHASE', @refId, 0.0, @credit, @balAfter, @notes, CURRENT_TIMESTAMP
                            );
                        ";
                        ledgerCmd.Parameters.AddWithValue("@supId", supplierId);
                        ledgerCmd.Parameters.AddWithValue("@refId", purchaseNumber);
                        ledgerCmd.Parameters.AddWithValue("@credit", grandTotal);
                        ledgerCmd.Parameters.AddWithValue("@balAfter", supplierBalBefore + grandTotal);
                        ledgerCmd.Parameters.AddWithValue("@notes", $"Confirmed Purchase {purchaseNumber}");
                        ledgerCmd.ExecuteNonQuery();
                    }

                    // If amount paid entered at purchase, record payment in ledger too
                    if (amountPaid > 0)
                    {
                        using (var ledgerPayCmd = conn.CreateCommand())
                        {
                            ledgerPayCmd.Transaction = tx;
                            ledgerPayCmd.CommandText = @"
                                INSERT INTO supplier_ledger (
                                    supplier_id, transaction_type, reference_id, debit, credit, balance_after, notes, transaction_date
                                ) VALUES (
                                    @supId, 'PAYMENT', @refId, @debit, 0.0, @balAfter, @notes, CURRENT_TIMESTAMP
                                );
                            ";
                            ledgerPayCmd.Parameters.AddWithValue("@supId", supplierId);
                            ledgerPayCmd.Parameters.AddWithValue("@refId", purchaseNumber);
                            ledgerPayCmd.Parameters.AddWithValue("@debit", amountPaid);
                            ledgerPayCmd.Parameters.AddWithValue("@balAfter", supplierBalAfter);
                            ledgerPayCmd.Parameters.AddWithValue("@notes", $"Initial Payment at Purchase {purchaseNumber}");
                            ledgerPayCmd.ExecuteNonQuery();
                        }
                    }

                    // 5. Update purchase status to Confirmed
                    using (var statusCmd = conn.CreateCommand())
                    {
                        statusCmd.Transaction = tx;
                        statusCmd.CommandText = "UPDATE purchases SET status = 'Confirmed', confirmed_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                        statusCmd.Parameters.AddWithValue("@id", purchaseId);
                        statusCmd.ExecuteNonQuery();
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

        public bool CancelPurchase(long purchaseId, out string errorMessage)
        {
            errorMessage = null;

            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    string currentStatus;
                    string purchaseNumber;
                    long supplierId;
                    decimal grandTotal;
                    decimal amountPaid;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "SELECT status, purchase_number, supplier_id, grand_total, amount_paid FROM purchases WHERE id = @id;";
                        cmd.Parameters.AddWithValue("@id", purchaseId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                errorMessage = "Purchase record not found.";
                                return false;
                            }
                            currentStatus = r["status"].ToString();
                            purchaseNumber = r["purchase_number"].ToString();
                            supplierId = Convert.ToInt64(r["supplier_id"]);
                            grandTotal = Convert.ToDecimal(r["grand_total"]);
                            amountPaid = Convert.ToDecimal(r["amount_paid"]);
                        }
                    }

                    if (currentStatus != "Confirmed")
                    {
                        errorMessage = $"Only Confirmed purchases can be cancelled. Current status is '{currentStatus}'.";
                        return false;
                    }

                    var items = GetPurchaseItems(conn, purchaseId, tx);

                    // 1. Verify stock availability (Prevent negative stock caused by reversal!)
                    foreach (var item in items)
                    {
                        decimal currentStock = 0;
                        using (var stockCmd = conn.CreateCommand())
                        {
                            stockCmd.Transaction = tx;
                            stockCmd.CommandText = "SELECT current_stock FROM products WHERE id = @prodId;";
                            stockCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            currentStock = Convert.ToDecimal(stockCmd.ExecuteScalar());
                        }

                        decimal totalReceived = item.Quantity + item.FreeQuantity;
                        if (currentStock < totalReceived)
                        {
                            errorMessage = $"Cannot cancel purchase: Stock for '{item.ProductName}' would drop negative (Current: {currentStock}, Required: {totalReceived}).";
                            tx.Rollback();
                            return false;
                        }
                    }

                    // 2. Reverse stock & record reversal movement
                    foreach (var item in items)
                    {
                        decimal currentStock = 0;
                        using (var stockCmd = conn.CreateCommand())
                        {
                            stockCmd.Transaction = tx;
                            stockCmd.CommandText = "SELECT current_stock FROM products WHERE id = @prodId;";
                            stockCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            currentStock = Convert.ToDecimal(stockCmd.ExecuteScalar());
                        }

                        decimal totalReceived = item.Quantity + item.FreeQuantity;
                        decimal newStock = currentStock - totalReceived;

                        using (var updateStockCmd = conn.CreateCommand())
                        {
                            updateStockCmd.Transaction = tx;
                            updateStockCmd.CommandText = "UPDATE products SET current_stock = @newStock, updated_at = CURRENT_TIMESTAMP WHERE id = @prodId;";
                            updateStockCmd.Parameters.AddWithValue("@newStock", newStock);
                            updateStockCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            updateStockCmd.ExecuteNonQuery();
                        }

                        using (var moveCmd = conn.CreateCommand())
                        {
                            moveCmd.Transaction = tx;
                            moveCmd.CommandText = @"
                                INSERT INTO stock_movements (
                                    product_id, reference_type, reference_id, quantity,
                                    quantity_before, quantity_after, notes, created_at
                                ) VALUES (
                                    @prodId, 'PURCHASE_CANCEL', @refId, @qty,
                                    @before, @after, @notes, CURRENT_TIMESTAMP
                                );
                            ";
                            moveCmd.Parameters.AddWithValue("@prodId", item.ProductId);
                            moveCmd.Parameters.AddWithValue("@refId", purchaseNumber);
                            moveCmd.Parameters.AddWithValue("@qty", -totalReceived);
                            moveCmd.Parameters.AddWithValue("@before", currentStock);
                            moveCmd.Parameters.AddWithValue("@after", newStock);
                            moveCmd.Parameters.AddWithValue("@notes", $"Reversed via Cancel of Purchase {purchaseNumber}");
                            moveCmd.ExecuteNonQuery();
                        }
                    }

                    // 3. Reverse supplier balance
                    decimal netPayableAddition = grandTotal - amountPaid;
                    decimal supplierBalBefore = 0;
                    using (var supCmd = conn.CreateCommand())
                    {
                        supCmd.Transaction = tx;
                        supCmd.CommandText = "SELECT current_balance FROM suppliers WHERE id = @supId;";
                        supCmd.Parameters.AddWithValue("@supId", supplierId);
                        supplierBalBefore = Convert.ToDecimal(supCmd.ExecuteScalar());
                    }

                    decimal supplierBalAfter = supplierBalBefore - netPayableAddition;
                    using (var updateSupCmd = conn.CreateCommand())
                    {
                        updateSupCmd.Transaction = tx;
                        updateSupCmd.CommandText = "UPDATE suppliers SET current_balance = @newBal, updated_at = CURRENT_TIMESTAMP WHERE id = @supId;";
                        updateSupCmd.Parameters.AddWithValue("@newBal", supplierBalAfter);
                        updateSupCmd.Parameters.AddWithValue("@supId", supplierId);
                        updateSupCmd.ExecuteNonQuery();
                    }

                    // Record ledger reversal
                    using (var ledgerCmd = conn.CreateCommand())
                    {
                        ledgerCmd.Transaction = tx;
                        ledgerCmd.CommandText = @"
                            INSERT INTO supplier_ledger (
                                supplier_id, transaction_type, reference_id, debit, credit, balance_after, notes, transaction_date
                            ) VALUES (
                                @supId, 'PURCHASE_CANCEL', @refId, @debit, 0.0, @balAfter, @notes, CURRENT_TIMESTAMP
                            );
                        ";
                        ledgerCmd.Parameters.AddWithValue("@supId", supplierId);
                        ledgerCmd.Parameters.AddWithValue("@refId", purchaseNumber);
                        ledgerCmd.Parameters.AddWithValue("@debit", netPayableAddition);
                        ledgerCmd.Parameters.AddWithValue("@balAfter", supplierBalAfter);
                        ledgerCmd.Parameters.AddWithValue("@notes", $"Cancellation of Purchase {purchaseNumber}");
                        ledgerCmd.ExecuteNonQuery();
                    }

                    // 4. Update status to Cancelled
                    using (var statusCmd = conn.CreateCommand())
                    {
                        statusCmd.Transaction = tx;
                        statusCmd.CommandText = "UPDATE purchases SET status = 'Cancelled', cancelled_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                        statusCmd.Parameters.AddWithValue("@id", purchaseId);
                        statusCmd.ExecuteNonQuery();
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

        public string GenerateNextPurchaseNumber()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM purchases;";
                int count = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                return $"PUR-{DateTime.Now.Year}-{count:D4}";
            }
        }

        private static List<PurchaseItem> GetPurchaseItems(SQLiteConnection conn, long purchaseId, SQLiteTransaction tx = null)
        {
            var list = new List<PurchaseItem>();
            using (var cmd = conn.CreateCommand())
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.CommandText = @"
                    SELECT pi.*, p.product_code, p.barcode, p.name_en AS product_name, c.name AS category_name
                    FROM purchase_items pi
                    JOIN products p ON pi.product_id = p.id
                    LEFT JOIN categories c ON p.category_id = c.id
                    WHERE pi.purchase_id = @purchaseId
                    ORDER BY pi.id ASC;
                ";
                cmd.Parameters.AddWithValue("@purchaseId", purchaseId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new PurchaseItem
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            PurchaseId = Convert.ToInt64(reader["purchase_id"]),
                            ProductId = Convert.ToInt64(reader["product_id"]),
                            ProductCode = reader["product_code"].ToString(),
                            Barcode = reader["barcode"].ToString(),
                            ProductName = reader["product_name"].ToString(),
                            CategoryName = reader["category_name"] == DBNull.Value ? "" : reader["category_name"].ToString(),
                            Quantity = Convert.ToDecimal(reader["quantity"]),
                            FreeQuantity = Convert.ToDecimal(reader["free_quantity"]),
                            PurchaseRate = Convert.ToDecimal(reader["purchase_rate"]),
                            DiscountPercent = Convert.ToDecimal(reader["discount_percent"]),
                            DiscountAmount = Convert.ToDecimal(reader["discount_amount"]),
                            TaxRate = Convert.ToDecimal(reader["tax_rate"]),
                            TaxableAmount = Convert.ToDecimal(reader["taxable_amount"]),
                            TaxAmount = Convert.ToDecimal(reader["tax_amount"]),
                            LineTotal = Convert.ToDecimal(reader["line_total"]),
                            BatchNumber = reader["batch_number"] == DBNull.Value ? null : reader["batch_number"].ToString(),
                            ExpiryDate = reader["expiry_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["expiry_date"])
                        });
                    }
                }
            }
            return list;
        }

        private static void InsertLineItems(SQLiteConnection conn, SQLiteTransaction tx, long purchaseId, IEnumerable<PurchaseItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO purchase_items (
                            purchase_id, product_id, quantity, free_quantity, purchase_rate,
                            discount_percent, discount_amount, tax_rate, taxable_amount, tax_amount,
                            line_total, batch_number, expiry_date
                        ) VALUES (
                            @pId, @prodId, @qty, @freeQty, @rate,
                            @discPct, @discAmt, @taxRate, @taxable, @taxAmt,
                            @total, @batch, @exp
                        );
                    ";
                    cmd.Parameters.AddWithValue("@pId", purchaseId);
                    cmd.Parameters.AddWithValue("@prodId", item.ProductId);
                    cmd.Parameters.AddWithValue("@qty", item.Quantity);
                    cmd.Parameters.AddWithValue("@freeQty", item.FreeQuantity);
                    cmd.Parameters.AddWithValue("@rate", item.PurchaseRate);
                    cmd.Parameters.AddWithValue("@discPct", item.DiscountPercent);
                    cmd.Parameters.AddWithValue("@discAmt", item.DiscountAmount);
                    cmd.Parameters.AddWithValue("@taxRate", item.TaxRate);
                    cmd.Parameters.AddWithValue("@taxable", item.TaxableAmount);
                    cmd.Parameters.AddWithValue("@taxAmt", item.TaxAmount);
                    cmd.Parameters.AddWithValue("@total", item.LineTotal);
                    cmd.Parameters.AddWithValue("@batch", (object)item.BatchNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@exp", item.ExpiryDate.HasValue ? (object)item.ExpiryDate.Value : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void AddPurchaseHeaderParameters(SQLiteCommand cmd, PurchaseHeader p)
        {
            cmd.Parameters.AddWithValue("@num", p.PurchaseNumber);
            cmd.Parameters.AddWithValue("@supId", p.SupplierId);
            cmd.Parameters.AddWithValue("@invNum", (object)p.SupplierInvoiceNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@invDate", p.SupplierInvoiceDate.HasValue ? (object)p.SupplierInvoiceDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@recDate", p.GoodsReceivedDate);
            cmd.Parameters.AddWithValue("@type", p.PurchaseType ?? "Credit");
            cmd.Parameters.AddWithValue("@dueDate", p.PaymentDueDate.HasValue ? (object)p.PaymentDueDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@refNum", (object)p.ReferenceNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)p.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tQty", p.TotalQty);
            cmd.Parameters.AddWithValue("@tFreeQty", p.TotalFreeQty);
            cmd.Parameters.AddWithValue("@gross", p.GrossAmount);
            cmd.Parameters.AddWithValue("@itemDisc", p.ItemDiscount);
            cmd.Parameters.AddWithValue("@addDisc", p.AdditionalDiscount);
            cmd.Parameters.AddWithValue("@taxable", p.TaxableAmount);
            cmd.Parameters.AddWithValue("@tax", p.TaxAmount);
            cmd.Parameters.AddWithValue("@addCharges", p.AdditionalCharges);
            cmd.Parameters.AddWithValue("@roundOff", p.RoundOff);
            cmd.Parameters.AddWithValue("@grand", p.GrandTotal);
            cmd.Parameters.AddWithValue("@paid", p.AmountPaid);
            cmd.Parameters.AddWithValue("@balDue", p.BalanceDue);
            cmd.Parameters.AddWithValue("@createdBy", p.CreatedBy ?? "Admin");
        }

        private static PurchaseHeader MapPurchaseHeader(IDataRecord r)
        {
            return new PurchaseHeader
            {
                Id = Convert.ToInt64(r["id"]),
                PurchaseNumber = r["purchase_number"].ToString(),
                SupplierId = Convert.ToInt64(r["supplier_id"]),
                SupplierName = r["supplier_name"] == DBNull.Value ? "" : r["supplier_name"].ToString(),
                SupplierInvoiceNumber = r["supplier_invoice_number"] == DBNull.Value ? null : r["supplier_invoice_number"].ToString(),
                SupplierInvoiceDate = r["supplier_invoice_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["supplier_invoice_date"]),
                GoodsReceivedDate = Convert.ToDateTime(r["goods_received_date"]),
                PurchaseType = r["purchase_type"].ToString(),
                PaymentDueDate = r["payment_due_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["payment_due_date"]),
                ReferenceNumber = r["reference_number"] == DBNull.Value ? null : r["reference_number"].ToString(),
                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                Status = r["status"].ToString(),
                TotalQty = Convert.ToDecimal(r["total_qty"]),
                TotalFreeQty = Convert.ToDecimal(r["total_free_qty"]),
                GrossAmount = Convert.ToDecimal(r["gross_amount"]),
                ItemDiscount = Convert.ToDecimal(r["item_discount"]),
                AdditionalDiscount = Convert.ToDecimal(r["additional_discount"]),
                TaxableAmount = Convert.ToDecimal(r["taxable_amount"]),
                TaxAmount = Convert.ToDecimal(r["tax_amount"]),
                AdditionalCharges = Convert.ToDecimal(r["additional_charges"]),
                RoundOff = Convert.ToDecimal(r["round_off"]),
                GrandTotal = Convert.ToDecimal(r["grand_total"]),
                AmountPaid = Convert.ToDecimal(r["amount_paid"]),
                BalanceDue = Convert.ToDecimal(r["balance_due"]),
                CreatedBy = r["created_by"] == DBNull.Value ? "Admin" : r["created_by"].ToString(),
                ConfirmedAt = r["confirmed_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["confirmed_at"]),
                CancelledAt = r["cancelled_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["cancelled_at"]),
                CreatedAt = Convert.ToDateTime(r["created_at"]),
                UpdatedAt = Convert.ToDateTime(r["updated_at"])
            };
        }
    }
}
