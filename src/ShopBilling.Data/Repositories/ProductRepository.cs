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
    public class ProductRepository : IProductRepository
    {
        public Product GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT p.*, c.name AS category_name, s.name AS subcategory_name, b.name AS brand_name
                    FROM products p
                    LEFT JOIN categories c ON p.category_id = c.id
                    LEFT JOIN subcategories s ON p.subcategory_id = s.id
                    LEFT JOIN brands b ON p.brand_id = b.id
                    WHERE p.id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) return MapProduct(reader);
                }
            }
            return null;
        }

        public Product GetByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT p.*, c.name AS category_name, s.name AS subcategory_name, b.name AS brand_name
                    FROM products p
                    LEFT JOIN categories c ON p.category_id = c.id
                    LEFT JOIN subcategories s ON p.subcategory_id = s.id
                    LEFT JOIN brands b ON p.brand_id = b.id
                    WHERE p.barcode = @barcode AND p.is_active = 1
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@barcode", barcode.Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) return MapProduct(reader);
                }
            }
            return null;
        }

        public Product GetByProductCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT p.*, c.name AS category_name, s.name AS subcategory_name, b.name AS brand_name
                    FROM products p
                    LEFT JOIN categories c ON p.category_id = c.id
                    LEFT JOIN subcategories s ON p.subcategory_id = s.id
                    LEFT JOIN brands b ON p.brand_id = b.id
                    WHERE p.product_code = @code
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@code", code.Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) return MapProduct(reader);
                }
            }
            return null;
        }

        public PagedResult<Product> Search(ProductFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var result = new PagedResult<Product>
            {
                PageNumber = Math.Max(1, filter.PageNumber),
                PageSize = Math.Max(1, filter.PageSize)
            };

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var whereClause = new StringBuilder(" WHERE 1=1 ");
                var parameters = new List<SQLiteParameter>();

                if (filter.IsActive.HasValue)
                {
                    whereClause.Append(" AND p.is_active = @isActive ");
                    parameters.Add(new SQLiteParameter("@isActive", filter.IsActive.Value ? 1 : 0));
                }

                if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
                {
                    whereClause.Append(" AND p.category_id = @catId ");
                    parameters.Add(new SQLiteParameter("@catId", filter.CategoryId.Value));
                }

                if (filter.SubcategoryId.HasValue && filter.SubcategoryId.Value > 0)
                {
                    whereClause.Append(" AND p.subcategory_id = @subcatId ");
                    parameters.Add(new SQLiteParameter("@subcatId", filter.SubcategoryId.Value));
                }

                if (filter.BrandId.HasValue && filter.BrandId.Value > 0)
                {
                    whereClause.Append(" AND p.brand_id = @brandId ");
                    parameters.Add(new SQLiteParameter("@brandId", filter.BrandId.Value));
                }

                if (filter.IsLowStockOnly.HasValue && filter.IsLowStockOnly.Value)
                {
                    whereClause.Append(" AND p.current_stock <= p.reorder_level ");
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    var term = filter.SearchTerm.Trim();
                    whereClause.Append(" AND (p.barcode = @exactTerm OR p.product_code = @exactTerm OR p.name_en LIKE @likeTerm OR p.name_ta LIKE @likeTerm) ");
                    parameters.Add(new SQLiteParameter("@exactTerm", term));
                    parameters.Add(new SQLiteParameter("@likeTerm", $"%{term}%"));
                }

                // 1. Get total count
                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM products p {whereClause}";
                    foreach (var p in parameters) countCmd.Parameters.Add(p);
                    result.TotalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // 2. Sorting
                string sortColumn = "p.name_en";
                switch (filter.SortBy?.ToLower())
                {
                    case "barcode": sortColumn = "p.barcode"; break;
                    case "product_code": sortColumn = "p.product_code"; break;
                    case "sale_rate": sortColumn = "p.sale_rate"; break;
                    case "current_stock": sortColumn = "p.current_stock"; break;
                    case "id": sortColumn = "p.id"; break;
                }
                string sortDirection = filter.SortDescending ? "DESC" : "ASC";

                // 3. Paged query with LIMIT and OFFSET
                int offset = (result.PageNumber - 1) * result.PageSize;

                using (var queryCmd = conn.CreateCommand())
                {
                    queryCmd.CommandText = $@"
                        SELECT p.*, c.name AS category_name, s.name AS subcategory_name, b.name AS brand_name
                        FROM products p
                        LEFT JOIN categories c ON p.category_id = c.id
                        LEFT JOIN subcategories s ON p.subcategory_id = s.id
                        LEFT JOIN brands b ON p.brand_id = b.id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        LIMIT @limit OFFSET @offset;
                    ";

                    foreach (var p in parameters)
                    {
                        queryCmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    }
                    queryCmd.Parameters.AddWithValue("@limit", result.PageSize);
                    queryCmd.Parameters.AddWithValue("@offset", offset);

                    using (var reader = queryCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Items.Add(MapProduct(reader));
                        }
                    }
                }
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        public long Insert(Product p)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO products (
                        product_code, barcode, name_en, name_ta, category_id, subcategory_id, brand_id,
                        unit, hsn_code, purchase_rate, sale_rate, wholesale_rate, mrp, tax_rate,
                        opening_stock, current_stock, reorder_level, max_stock_level, is_active,
                        created_at, updated_at
                    ) VALUES (
                        @code, @barcode, @nameEn, @nameTa, @catId, @subcatId, @brandId,
                        @unit, @hsn, @purRate, @saleRate, @wsRate, @mrp, @taxRate,
                        @opStock, @curStock, @reorder, @maxStock, @isActive,
                        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                    );
                    SELECT last_insert_rowid();
                ";

                AddProductParameters(cmd, p);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool Update(Product p)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE products SET
                        product_code = @code,
                        barcode = @barcode,
                        name_en = @nameEn,
                        name_ta = @nameTa,
                        category_id = @catId,
                        subcategory_id = @subcatId,
                        brand_id = @brandId,
                        unit = @unit,
                        hsn_code = @hsn,
                        purchase_rate = @purRate,
                        sale_rate = @saleRate,
                        wholesale_rate = @wsRate,
                        mrp = @mrp,
                        tax_rate = @taxRate,
                        opening_stock = @opStock,
                        current_stock = @curStock,
                        reorder_level = @reorder,
                        max_stock_level = @maxStock,
                        is_active = @isActive,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @id;
                ";

                AddProductParameters(cmd, p);
                cmd.Parameters.AddWithValue("@id", p.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Deactivate(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE products SET is_active = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Activate(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE products SET is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public int BulkInsert(IEnumerable<Product> products, bool updateDuplicates = false)
        {
            int count = 0;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                string sql = updateDuplicates
                    ? @"INSERT OR REPLACE INTO products (
                            product_code, barcode, name_en, name_ta, category_id, subcategory_id, brand_id,
                            unit, hsn_code, purchase_rate, sale_rate, wholesale_rate, mrp, tax_rate,
                            opening_stock, current_stock, reorder_level, max_stock_level, is_active,
                            created_at, updated_at
                        ) VALUES (
                            @code, @barcode, @nameEn, @nameTa, @catId, @subcatId, @brandId,
                            @unit, @hsn, @purRate, @saleRate, @wsRate, @mrp, @taxRate,
                            @opStock, @curStock, @reorder, @maxStock, @isActive,
                            CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                        );"
                    : @"INSERT OR IGNORE INTO products (
                            product_code, barcode, name_en, name_ta, category_id, subcategory_id, brand_id,
                            unit, hsn_code, purchase_rate, sale_rate, wholesale_rate, mrp, tax_rate,
                            opening_stock, current_stock, reorder_level, max_stock_level, is_active,
                            created_at, updated_at
                        ) VALUES (
                            @code, @barcode, @nameEn, @nameTa, @catId, @subcatId, @brandId,
                            @unit, @hsn, @purRate, @saleRate, @wsRate, @mrp, @taxRate,
                            @opStock, @curStock, @reorder, @maxStock, @isActive,
                            CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                        );";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;

                    var pCode = cmd.Parameters.Add("@code", DbType.String);
                    var pBarcode = cmd.Parameters.Add("@barcode", DbType.String);
                    var pNameEn = cmd.Parameters.Add("@nameEn", DbType.String);
                    var pNameTa = cmd.Parameters.Add("@nameTa", DbType.String);
                    var pCatId = cmd.Parameters.Add("@catId", DbType.Int64);
                    var pSubcatId = cmd.Parameters.Add("@subcatId", DbType.Int64);
                    var pBrandId = cmd.Parameters.Add("@brandId", DbType.Int64);
                    var pUnit = cmd.Parameters.Add("@unit", DbType.String);
                    var pHsn = cmd.Parameters.Add("@hsn", DbType.String);
                    var pPurRate = cmd.Parameters.Add("@purRate", DbType.Decimal);
                    var pSaleRate = cmd.Parameters.Add("@saleRate", DbType.Decimal);
                    var pWsRate = cmd.Parameters.Add("@wsRate", DbType.Decimal);
                    var pMrp = cmd.Parameters.Add("@mrp", DbType.Decimal);
                    var pTaxRate = cmd.Parameters.Add("@taxRate", DbType.Decimal);
                    var pOpStock = cmd.Parameters.Add("@opStock", DbType.Decimal);
                    var pCurStock = cmd.Parameters.Add("@curStock", DbType.Decimal);
                    var pReorder = cmd.Parameters.Add("@reorder", DbType.Decimal);
                    var pMaxStock = cmd.Parameters.Add("@maxStock", DbType.Decimal);
                    var pIsActive = cmd.Parameters.Add("@isActive", DbType.Int32);

                    foreach (var p in products)
                    {
                        pCode.Value = p.ProductCode ?? (object)DBNull.Value;
                        pBarcode.Value = p.Barcode ?? (object)DBNull.Value;
                        pNameEn.Value = p.NameEn ?? (object)DBNull.Value;
                        pNameTa.Value = (object)p.NameTa ?? DBNull.Value;
                        pCatId.Value = p.CategoryId.HasValue ? (object)p.CategoryId.Value : DBNull.Value;
                        pSubcatId.Value = p.SubcategoryId.HasValue ? (object)p.SubcategoryId.Value : DBNull.Value;
                        pBrandId.Value = p.BrandId.HasValue ? (object)p.BrandId.Value : DBNull.Value;
                        pUnit.Value = p.Unit ?? "PCS";
                        pHsn.Value = (object)p.HsnCode ?? DBNull.Value;
                        pPurRate.Value = p.PurchaseRate;
                        pSaleRate.Value = p.SaleRate;
                        pWsRate.Value = p.WholesaleRate;
                        pMrp.Value = p.Mrp;
                        pTaxRate.Value = p.TaxRate;
                        pOpStock.Value = p.OpeningStock;
                        pCurStock.Value = p.CurrentStock;
                        pReorder.Value = p.ReorderLevel;
                        pMaxStock.Value = p.MaxStockLevel;
                        pIsActive.Value = p.IsActive ? 1 : 0;

                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }

                tx.Commit();
            }
            return count;
        }

        public bool ExistsBarcode(string barcode, long? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return false;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = excludeId.HasValue
                    ? "SELECT COUNT(*) FROM products WHERE barcode = @barcode AND id != @id;"
                    : "SELECT COUNT(*) FROM products WHERE barcode = @barcode;";
                cmd.Parameters.AddWithValue("@barcode", barcode.Trim());
                if (excludeId.HasValue) cmd.Parameters.AddWithValue("@id", excludeId.Value);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public bool ExistsProductCode(string code, long? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = excludeId.HasValue
                    ? "SELECT COUNT(*) FROM products WHERE product_code = @code AND id != @id;"
                    : "SELECT COUNT(*) FROM products WHERE product_code = @code;";
                cmd.Parameters.AddWithValue("@code", code.Trim());
                if (excludeId.HasValue) cmd.Parameters.AddWithValue("@id", excludeId.Value);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public int GetTotalCount()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM products WHERE is_active = 1;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int GetLowStockCount()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM products WHERE is_active = 1 AND current_stock <= reorder_level;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static void AddProductParameters(SQLiteCommand cmd, Product p)
        {
            cmd.Parameters.AddWithValue("@code", p.ProductCode);
            cmd.Parameters.AddWithValue("@barcode", p.Barcode);
            cmd.Parameters.AddWithValue("@nameEn", p.NameEn);
            cmd.Parameters.AddWithValue("@nameTa", (object)p.NameTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@catId", p.CategoryId.HasValue ? (object)p.CategoryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@subcatId", p.SubcategoryId.HasValue ? (object)p.SubcategoryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@brandId", p.BrandId.HasValue ? (object)p.BrandId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@unit", p.Unit ?? "PCS");
            cmd.Parameters.AddWithValue("@hsn", (object)p.HsnCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@purRate", p.PurchaseRate);
            cmd.Parameters.AddWithValue("@saleRate", p.SaleRate);
            cmd.Parameters.AddWithValue("@wsRate", p.WholesaleRate);
            cmd.Parameters.AddWithValue("@mrp", p.Mrp);
            cmd.Parameters.AddWithValue("@taxRate", p.TaxRate);
            cmd.Parameters.AddWithValue("@opStock", p.OpeningStock);
            cmd.Parameters.AddWithValue("@curStock", p.CurrentStock);
            cmd.Parameters.AddWithValue("@reorder", p.ReorderLevel);
            cmd.Parameters.AddWithValue("@maxStock", p.MaxStockLevel);
            cmd.Parameters.AddWithValue("@isActive", p.IsActive ? 1 : 0);
        }

        private static Product MapProduct(IDataRecord r)
        {
            return new Product
            {
                Id = Convert.ToInt64(r["id"]),
                ProductCode = r["product_code"].ToString(),
                Barcode = r["barcode"].ToString(),
                NameEn = r["name_en"].ToString(),
                NameTa = r["name_ta"] == DBNull.Value ? null : r["name_ta"].ToString(),
                CategoryId = r["category_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["category_id"]),
                CategoryName = r["category_name"] == DBNull.Value ? null : r["category_name"].ToString(),
                SubcategoryId = r["subcategory_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["subcategory_id"]),
                SubcategoryName = r["subcategory_name"] == DBNull.Value ? null : r["subcategory_name"].ToString(),
                BrandId = r["brand_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["brand_id"]),
                BrandName = r["brand_name"] == DBNull.Value ? null : r["brand_name"].ToString(),
                Unit = r["unit"] == DBNull.Value ? "PCS" : r["unit"].ToString(),
                HsnCode = r["hsn_code"] == DBNull.Value ? null : r["hsn_code"].ToString(),
                PurchaseRate = Convert.ToDecimal(r["purchase_rate"]),
                SaleRate = Convert.ToDecimal(r["sale_rate"]),
                WholesaleRate = Convert.ToDecimal(r["wholesale_rate"]),
                Mrp = Convert.ToDecimal(r["mrp"]),
                TaxRate = Convert.ToDecimal(r["tax_rate"]),
                OpeningStock = Convert.ToDecimal(r["opening_stock"]),
                CurrentStock = Convert.ToDecimal(r["current_stock"]),
                ReorderLevel = Convert.ToDecimal(r["reorder_level"]),
                MaxStockLevel = Convert.ToDecimal(r["max_stock_level"]),
                IsActive = Convert.ToInt32(r["is_active"]) == 1,
                CreatedAt = Convert.ToDateTime(r["created_at"]),
                UpdatedAt = Convert.ToDateTime(r["updated_at"])
            };
        }
    }
}
