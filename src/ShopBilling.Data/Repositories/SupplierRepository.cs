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
    public class SupplierRepository : ISupplierRepository
    {
        public Supplier GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.*, 
                           (SELECT COUNT(*) FROM purchases p WHERE p.supplier_id = s.id) AS total_purchases
                    FROM suppliers s
                    WHERE s.id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) return MapSupplier(reader);
                }
            }
            return null;
        }

        public Supplier GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.*, 
                           (SELECT COUNT(*) FROM purchases p WHERE p.supplier_id = s.id) AS total_purchases
                    FROM suppliers s
                    WHERE s.code = @code
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@code", code.Trim());
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) return MapSupplier(reader);
                }
            }
            return null;
        }

        public IEnumerable<Supplier> GetAllActive()
        {
            var list = new List<Supplier>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.*, 0 AS total_purchases
                    FROM suppliers s
                    WHERE s.is_active = 1
                    ORDER BY s.name ASC;
                ";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapSupplier(reader));
                    }
                }
            }
            return list;
        }

        public PagedResult<Supplier> Search(SupplierFilter filter)
        {
            var sw = Stopwatch.StartNew();
            var result = new PagedResult<Supplier>
            {
                PageNumber = Math.Max(1, filter.PageNumber),
                PageSize = Math.Max(1, filter.PageSize)
            };

            using (var conn = DatabaseConnection.CreateConnection())
            {
                var where = new StringBuilder(" WHERE 1=1 ");
                var parameters = new List<SQLiteParameter>();

                if (filter.IsActive.HasValue)
                {
                    where.Append(" AND s.is_active = @isActive ");
                    parameters.Add(new SQLiteParameter("@isActive", filter.IsActive.Value ? 1 : 0));
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    var term = filter.SearchTerm.Trim();
                    where.Append(" AND (s.code LIKE @likeTerm OR s.name LIKE @likeTerm OR s.mobile LIKE @likeTerm OR s.gstin LIKE @likeTerm) ");
                    parameters.Add(new SQLiteParameter("@likeTerm", $"%{term}%"));
                }

                // Total count
                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM suppliers s {where}";
                    foreach (var p in parameters) countCmd.Parameters.Add(p);
                    result.TotalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                string sortCol = "s.name";
                switch (filter.SortBy?.ToLower())
                {
                    case "code": sortCol = "s.code"; break;
                    case "balance": sortCol = "s.current_balance"; break;
                    case "city": sortCol = "s.city"; break;
                    case "id": sortCol = "s.id"; break;
                }
                string sortDir = filter.SortDescending ? "DESC" : "ASC";
                int offset = (result.PageNumber - 1) * result.PageSize;

                using (var queryCmd = conn.CreateCommand())
                {
                    queryCmd.CommandText = $@"
                        SELECT s.*, 
                               (SELECT COUNT(*) FROM purchases p WHERE p.supplier_id = s.id) AS total_purchases
                        FROM suppliers s
                        {where}
                        ORDER BY {sortCol} {sortDir}
                        LIMIT @limit OFFSET @offset;
                    ";
                    foreach (var p in parameters) queryCmd.Parameters.Add(new SQLiteParameter(p.ParameterName, p.Value));
                    queryCmd.Parameters.AddWithValue("@limit", result.PageSize);
                    queryCmd.Parameters.AddWithValue("@offset", offset);

                    using (var reader = queryCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Items.Add(MapSupplier(reader));
                        }
                    }
                }
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        public long Insert(Supplier s)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO suppliers (
                        code, name, contact_person, mobile, alt_mobile, whatsapp, email,
                        gstin, pan, address, city, state, state_code, pin_code,
                        opening_balance, current_balance, payment_terms, notes, is_active,
                        created_at, updated_at
                    ) VALUES (
                        @code, @name, @contact, @mobile, @altMobile, @whatsapp, @email,
                        @gstin, @pan, @address, @city, @state, @stateCode, @pin,
                        @opBal, @curBal, @terms, @notes, @isActive,
                        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                    );
                    SELECT last_insert_rowid();
                ";
                AddSupplierParams(cmd, s);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool Update(Supplier s)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE suppliers SET
                        code = @code,
                        name = @name,
                        contact_person = @contact,
                        mobile = @mobile,
                        alt_mobile = @altMobile,
                        whatsapp = @whatsapp,
                        email = @email,
                        gstin = @gstin,
                        pan = @pan,
                        address = @address,
                        city = @city,
                        state = @state,
                        state_code = @stateCode,
                        pin_code = @pin,
                        opening_balance = @opBal,
                        payment_terms = @terms,
                        notes = @notes,
                        is_active = @isActive,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @id;
                ";
                AddSupplierParams(cmd, s);
                cmd.Parameters.AddWithValue("@id", s.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Deactivate(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE suppliers SET is_active = 0, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Activate(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE suppliers SET is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool ExistsCode(string code, long? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = excludeId.HasValue
                    ? "SELECT COUNT(*) FROM suppliers WHERE code = @code AND id != @id;"
                    : "SELECT COUNT(*) FROM suppliers WHERE code = @code;";
                cmd.Parameters.AddWithValue("@code", code.Trim());
                if (excludeId.HasValue) cmd.Parameters.AddWithValue("@id", excludeId.Value);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public bool HasTransactions(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT (SELECT COUNT(*) FROM purchases WHERE supplier_id = @id) + 
                           (SELECT COUNT(*) FROM supplier_payments WHERE supplier_id = @id);
                ";
                cmd.Parameters.AddWithValue("@id", id);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public int BulkInsert(IEnumerable<Supplier> suppliers, bool updateDuplicates = false)
        {
            int count = 0;
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                string sql = updateDuplicates
                    ? @"INSERT OR REPLACE INTO suppliers (
                            code, name, contact_person, mobile, alt_mobile, whatsapp, email,
                            gstin, pan, address, city, state, state_code, pin_code,
                            opening_balance, current_balance, payment_terms, notes, is_active,
                            created_at, updated_at
                        ) VALUES (
                            @code, @name, @contact, @mobile, @altMobile, @whatsapp, @email,
                            @gstin, @pan, @address, @city, @state, @stateCode, @pin,
                            @opBal, @curBal, @terms, @notes, @isActive,
                            CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                        );"
                    : @"INSERT OR IGNORE INTO suppliers (
                            code, name, contact_person, mobile, alt_mobile, whatsapp, email,
                            gstin, pan, address, city, state, state_code, pin_code,
                            opening_balance, current_balance, payment_terms, notes, is_active,
                            created_at, updated_at
                        ) VALUES (
                            @code, @name, @contact, @mobile, @altMobile, @whatsapp, @email,
                            @gstin, @pan, @address, @city, @state, @stateCode, @pin,
                            @opBal, @curBal, @terms, @notes, @isActive,
                            CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                        );";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;

                    var pCode = cmd.Parameters.Add("@code", DbType.String);
                    var pName = cmd.Parameters.Add("@name", DbType.String);
                    var pContact = cmd.Parameters.Add("@contact", DbType.String);
                    var pMobile = cmd.Parameters.Add("@mobile", DbType.String);
                    var pAltMobile = cmd.Parameters.Add("@altMobile", DbType.String);
                    var pWhatsApp = cmd.Parameters.Add("@whatsapp", DbType.String);
                    var pEmail = cmd.Parameters.Add("@email", DbType.String);
                    var pGstin = cmd.Parameters.Add("@gstin", DbType.String);
                    var pPan = cmd.Parameters.Add("@pan", DbType.String);
                    var pAddr = cmd.Parameters.Add("@address", DbType.String);
                    var pCity = cmd.Parameters.Add("@city", DbType.String);
                    var pState = cmd.Parameters.Add("@state", DbType.String);
                    var pStateCode = cmd.Parameters.Add("@stateCode", DbType.String);
                    var pPin = cmd.Parameters.Add("@pin", DbType.String);
                    var pOpBal = cmd.Parameters.Add("@opBal", DbType.Decimal);
                    var pCurBal = cmd.Parameters.Add("@curBal", DbType.Decimal);
                    var pTerms = cmd.Parameters.Add("@terms", DbType.String);
                    var pNotes = cmd.Parameters.Add("@notes", DbType.String);
                    var pActive = cmd.Parameters.Add("@isActive", DbType.Int32);

                    foreach (var s in suppliers)
                    {
                        pCode.Value = s.Code ?? "";
                        pName.Value = s.Name ?? "";
                        pContact.Value = (object)s.ContactPerson ?? DBNull.Value;
                        pMobile.Value = (object)s.Mobile ?? DBNull.Value;
                        pAltMobile.Value = (object)s.AltMobile ?? DBNull.Value;
                        pWhatsApp.Value = (object)s.WhatsApp ?? DBNull.Value;
                        pEmail.Value = (object)s.Email ?? DBNull.Value;
                        pGstin.Value = (object)s.Gstin ?? DBNull.Value;
                        pPan.Value = (object)s.Pan ?? DBNull.Value;
                        pAddr.Value = (object)s.Address ?? DBNull.Value;
                        pCity.Value = (object)s.City ?? DBNull.Value;
                        pState.Value = s.State ?? "Tamil Nadu";
                        pStateCode.Value = s.StateCode ?? "33";
                        pPin.Value = (object)s.PinCode ?? DBNull.Value;
                        pOpBal.Value = s.OpeningBalance;
                        pCurBal.Value = s.CurrentBalance;
                        pTerms.Value = (object)s.PaymentTerms ?? DBNull.Value;
                        pNotes.Value = (object)s.Notes ?? DBNull.Value;
                        pActive.Value = s.IsActive ? 1 : 0;

                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
                tx.Commit();
            }
            return count;
        }

        private static void AddSupplierParams(SQLiteCommand cmd, Supplier s)
        {
            cmd.Parameters.AddWithValue("@code", s.Code);
            cmd.Parameters.AddWithValue("@name", s.Name);
            cmd.Parameters.AddWithValue("@contact", (object)s.ContactPerson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mobile", (object)s.Mobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@altMobile", (object)s.AltMobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@whatsapp", (object)s.WhatsApp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object)s.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@gstin", (object)s.Gstin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pan", (object)s.Pan ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@address", (object)s.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@city", (object)s.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@state", s.State ?? "Tamil Nadu");
            cmd.Parameters.AddWithValue("@stateCode", s.StateCode ?? "33");
            cmd.Parameters.AddWithValue("@pin", (object)s.PinCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@opBal", s.OpeningBalance);
            cmd.Parameters.AddWithValue("@curBal", s.CurrentBalance);
            cmd.Parameters.AddWithValue("@terms", (object)s.PaymentTerms ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)s.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isActive", s.IsActive ? 1 : 0);
        }

        private static Supplier MapSupplier(IDataRecord r)
        {
            return new Supplier
            {
                Id = Convert.ToInt64(r["id"]),
                Code = r["code"].ToString(),
                Name = r["name"].ToString(),
                ContactPerson = r["contact_person"] == DBNull.Value ? null : r["contact_person"].ToString(),
                Mobile = r["mobile"] == DBNull.Value ? null : r["mobile"].ToString(),
                AltMobile = r["alt_mobile"] == DBNull.Value ? null : r["alt_mobile"].ToString(),
                WhatsApp = r["whatsapp"] == DBNull.Value ? null : r["whatsapp"].ToString(),
                Email = r["email"] == DBNull.Value ? null : r["email"].ToString(),
                Gstin = r["gstin"] == DBNull.Value ? null : r["gstin"].ToString(),
                Pan = r["pan"] == DBNull.Value ? null : r["pan"].ToString(),
                Address = r["address"] == DBNull.Value ? null : r["address"].ToString(),
                City = r["city"] == DBNull.Value ? null : r["city"].ToString(),
                State = r["state"] == DBNull.Value ? "Tamil Nadu" : r["state"].ToString(),
                StateCode = r["state_code"] == DBNull.Value ? "33" : r["state_code"].ToString(),
                PinCode = r["pin_code"] == DBNull.Value ? null : r["pin_code"].ToString(),
                OpeningBalance = Convert.ToDecimal(r["opening_balance"]),
                CurrentBalance = Convert.ToDecimal(r["current_balance"]),
                PaymentTerms = r["payment_terms"] == DBNull.Value ? null : r["payment_terms"].ToString(),
                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                IsActive = Convert.ToInt32(r["is_active"]) == 1,
                CreatedAt = Convert.ToDateTime(r["created_at"]),
                UpdatedAt = Convert.ToDateTime(r["updated_at"]),
                TotalPurchasesCount = r["total_purchases"] == DBNull.Value ? 0 : Convert.ToInt32(r["total_purchases"])
            };
        }
    }
}
