using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Data.Database;

namespace ShopBilling.Data.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        public IEnumerable<Category> GetAll(bool includeInactive = false)
        {
            var list = new List<Category>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT c.*, (SELECT COUNT(*) FROM products p WHERE p.category_id = c.id AND p.is_active = 1) AS product_count
                    FROM categories c
                    " + (includeInactive ? "" : "WHERE c.is_active = 1 ") + @"
                    ORDER BY c.name ASC;
                ";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Category
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1,
                            ProductCount = Convert.ToInt32(r["product_count"])
                        });
                    }
                }
            }
            return list;
        }

        public Category GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM categories WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new Category
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public Category GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM categories WHERE name = @name COLLATE NOCASE LIMIT 1;";
                cmd.Parameters.AddWithValue("@name", name.Trim());
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new Category
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public long Insert(Category cat)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO categories (code, name, is_active) VALUES (@code, @name, @active);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@code", cat.Code);
                cmd.Parameters.AddWithValue("@name", cat.Name);
                cmd.Parameters.AddWithValue("@active", cat.IsActive ? 1 : 0);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool Update(Category cat)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE categories SET code = @code, name = @name, is_active = @active WHERE id = @id;";
                cmd.Parameters.AddWithValue("@code", cat.Code);
                cmd.Parameters.AddWithValue("@name", cat.Name);
                cmd.Parameters.AddWithValue("@active", cat.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", cat.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Delete(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE categories SET is_active = 0 WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public IEnumerable<Subcategory> GetSubcategories(long categoryId, bool includeInactive = false)
        {
            var list = new List<Subcategory>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.*, c.name AS category_name,
                        (SELECT COUNT(*) FROM products p WHERE p.subcategory_id = s.id AND p.is_active = 1) AS product_count
                    FROM subcategories s
                    JOIN categories c ON s.category_id = c.id
                    WHERE s.category_id = @catId " + (includeInactive ? "" : "AND s.is_active = 1 ") + @"
                    ORDER BY s.name ASC;
                ";
                cmd.Parameters.AddWithValue("@catId", categoryId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Subcategory
                        {
                            Id = Convert.ToInt64(r["id"]),
                            CategoryId = Convert.ToInt64(r["category_id"]),
                            CategoryName = r["category_name"].ToString(),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1,
                            ProductCount = Convert.ToInt32(r["product_count"])
                        });
                    }
                }
            }
            return list;
        }

        public Subcategory GetSubcategoryById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.*, c.name AS category_name
                    FROM subcategories s
                    JOIN categories c ON s.category_id = c.id
                    WHERE s.id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new Subcategory
                        {
                            Id = Convert.ToInt64(r["id"]),
                            CategoryId = Convert.ToInt64(r["category_id"]),
                            CategoryName = r["category_name"].ToString(),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public long InsertSubcategory(Subcategory s)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO subcategories (category_id, code, name, is_active) VALUES (@catId, @code, @name, @active);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@catId", s.CategoryId);
                cmd.Parameters.AddWithValue("@code", s.Code);
                cmd.Parameters.AddWithValue("@name", s.Name);
                cmd.Parameters.AddWithValue("@active", s.IsActive ? 1 : 0);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool UpdateSubcategory(Subcategory s)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE subcategories SET code = @code, name = @name, is_active = @active WHERE id = @id;";
                cmd.Parameters.AddWithValue("@code", s.Code);
                cmd.Parameters.AddWithValue("@name", s.Name);
                cmd.Parameters.AddWithValue("@active", s.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", s.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    public class BrandRepository : IBrandRepository
    {
        public IEnumerable<Brand> GetAll(bool includeInactive = false)
        {
            var list = new List<Brand>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT b.*, (SELECT COUNT(*) FROM products p WHERE p.brand_id = b.id AND p.is_active = 1) AS product_count
                    FROM brands b
                    " + (includeInactive ? "" : "WHERE b.is_active = 1 ") + @"
                    ORDER BY b.name ASC;
                ";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Brand
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1,
                            ProductCount = Convert.ToInt32(r["product_count"])
                        });
                    }
                }
            }
            return list;
        }

        public Brand GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM brands WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new Brand
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public Brand GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM brands WHERE name = @name COLLATE NOCASE LIMIT 1;";
                cmd.Parameters.AddWithValue("@name", name.Trim());
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new Brand
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Code = r["code"].ToString(),
                            Name = r["name"].ToString(),
                            IsActive = Convert.ToInt32(r["is_active"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public long Insert(Brand b)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO brands (code, name, is_active) VALUES (@code, @name, @active);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@code", b.Code);
                cmd.Parameters.AddWithValue("@name", b.Name);
                cmd.Parameters.AddWithValue("@active", b.IsActive ? 1 : 0);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool Update(Brand b)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE brands SET code = @code, name = @name, is_active = @active WHERE id = @id;";
                cmd.Parameters.AddWithValue("@code", b.Code);
                cmd.Parameters.AddWithValue("@name", b.Name);
                cmd.Parameters.AddWithValue("@active", b.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", b.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Delete(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE brands SET is_active = 0 WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        public void Log(string entity, string entityId, string action, string details)
        {
            try
            {
                using (var conn = DatabaseConnection.CreateConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO audit_logs (entity, entity_id, action, details, timestamp)
                        VALUES (@entity, @entityId, @action, @details, CURRENT_TIMESTAMP);
                    ";
                    cmd.Parameters.AddWithValue("@entity", entity);
                    cmd.Parameters.AddWithValue("@entityId", (object)entityId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@action", action);
                    cmd.Parameters.AddWithValue("@details", details);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* Ignore audit failure to not break main flow */ }
        }

        public IEnumerable<AuditLog> GetRecent(int limit = 100)
        {
            var list = new List<AuditLog>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM audit_logs ORDER BY timestamp DESC LIMIT @limit;";
                cmd.Parameters.AddWithValue("@limit", limit);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new AuditLog
                        {
                            Id = Convert.ToInt64(r["id"]),
                            Entity = r["entity"].ToString(),
                            EntityId = r["entity_id"] == DBNull.Value ? null : r["entity_id"].ToString(),
                            Action = r["action"].ToString(),
                            Details = r["details"] == DBNull.Value ? null : r["details"].ToString(),
                            Timestamp = Convert.ToDateTime(r["timestamp"])
                        });
                    }
                }
            }
            return list;
        }
    }
}
