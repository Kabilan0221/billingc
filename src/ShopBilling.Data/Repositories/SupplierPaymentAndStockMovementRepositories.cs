using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Data.Database;

namespace ShopBilling.Data.Repositories
{
    public class SupplierPaymentRepository : ISupplierPaymentRepository
    {
        public SupplierPayment GetById(long id)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT sp.*, s.name AS supplier_name, p.purchase_number
                    FROM supplier_payments sp
                    JOIN suppliers s ON sp.supplier_id = s.id
                    LEFT JOIN purchases p ON sp.purchase_id = p.id
                    WHERE sp.id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read()) return MapPayment(r);
                }
            }
            return null;
        }

        public IEnumerable<SupplierPayment> GetBySupplierId(long supplierId)
        {
            var list = new List<SupplierPayment>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT sp.*, s.name AS supplier_name, p.purchase_number
                    FROM supplier_payments sp
                    JOIN suppliers s ON sp.supplier_id = s.id
                    LEFT JOIN purchases p ON sp.purchase_id = p.id
                    WHERE sp.supplier_id = @supId
                    ORDER BY sp.payment_date DESC;
                ";
                cmd.Parameters.AddWithValue("@supId", supplierId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(MapPayment(r));
                }
            }
            return list;
        }

        public IEnumerable<SupplierPayment> GetByPurchaseId(long purchaseId)
        {
            var list = new List<SupplierPayment>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT sp.*, s.name AS supplier_name, p.purchase_number
                    FROM supplier_payments sp
                    JOIN suppliers s ON sp.supplier_id = s.id
                    LEFT JOIN purchases p ON sp.purchase_id = p.id
                    WHERE sp.purchase_id = @pId
                    ORDER BY sp.payment_date DESC;
                ";
                cmd.Parameters.AddWithValue("@pId", purchaseId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(MapPayment(r));
                }
            }
            return list;
        }

        public long InsertPayment(SupplierPayment payment)
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    long paymentId;

                    // 1. Insert payment record
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
                            INSERT INTO supplier_payments (
                                payment_number, supplier_id, purchase_id, payment_date,
                                payment_method, amount, reference_number, notes, created_at
                            ) VALUES (
                                @num, @supId, @pId, @date,
                                @method, @amount, @ref, @notes, CURRENT_TIMESTAMP
                            );
                            SELECT last_insert_rowid();
                        ";
                        cmd.Parameters.AddWithValue("@num", payment.PaymentNumber);
                        cmd.Parameters.AddWithValue("@supId", payment.SupplierId);
                        cmd.Parameters.AddWithValue("@pId", payment.PurchaseId.HasValue ? (object)payment.PurchaseId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@date", payment.PaymentDate);
                        cmd.Parameters.AddWithValue("@method", payment.PaymentMethod ?? "Cash");
                        cmd.Parameters.AddWithValue("@amount", payment.Amount);
                        cmd.Parameters.AddWithValue("@ref", (object)payment.ReferenceNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@notes", (object)payment.Notes ?? DBNull.Value);
                        paymentId = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    // 2. Fetch current balance
                    decimal currentBal = 0;
                    using (var balCmd = conn.CreateCommand())
                    {
                        balCmd.Transaction = tx;
                        balCmd.CommandText = "SELECT current_balance FROM suppliers WHERE id = @supId;";
                        balCmd.Parameters.AddWithValue("@supId", payment.SupplierId);
                        currentBal = Convert.ToDecimal(balCmd.ExecuteScalar());
                    }

                    decimal newBal = currentBal - payment.Amount;

                    // 3. Update supplier balance
                    using (var updateSupCmd = conn.CreateCommand())
                    {
                        updateSupCmd.Transaction = tx;
                        updateSupCmd.CommandText = "UPDATE suppliers SET current_balance = @newBal, updated_at = CURRENT_TIMESTAMP WHERE id = @supId;";
                        updateSupCmd.Parameters.AddWithValue("@newBal", newBal);
                        updateSupCmd.Parameters.AddWithValue("@supId", payment.SupplierId);
                        updateSupCmd.ExecuteNonQuery();
                    }

                    // 4. Update linked purchase if provided
                    if (payment.PurchaseId.HasValue && payment.PurchaseId.Value > 0)
                    {
                        using (var purCmd = conn.CreateCommand())
                        {
                            purCmd.Transaction = tx;
                            purCmd.CommandText = @"
                                UPDATE purchases 
                                SET amount_paid = amount_paid + @amt,
                                    balance_due = MAX(0, grand_total - (amount_paid + @amt)),
                                    updated_at = CURRENT_TIMESTAMP
                                WHERE id = @pId;
                            ";
                            purCmd.Parameters.AddWithValue("@amt", payment.Amount);
                            purCmd.Parameters.AddWithValue("@pId", payment.PurchaseId.Value);
                            purCmd.ExecuteNonQuery();
                        }
                    }

                    // 5. Record entry in supplier ledger
                    using (var ledgerCmd = conn.CreateCommand())
                    {
                        ledgerCmd.Transaction = tx;
                        ledgerCmd.CommandText = @"
                            INSERT INTO supplier_ledger (
                                supplier_id, transaction_type, reference_id, debit, credit, balance_after, notes, transaction_date
                            ) VALUES (
                                @supId, 'PAYMENT', @refId, @debit, 0.0, @balAfter, @notes, CURRENT_TIMESTAMP
                            );
                        ";
                        ledgerCmd.Parameters.AddWithValue("@supId", payment.SupplierId);
                        ledgerCmd.Parameters.AddWithValue("@refId", payment.PaymentNumber);
                        ledgerCmd.Parameters.AddWithValue("@debit", payment.Amount);
                        ledgerCmd.Parameters.AddWithValue("@balAfter", newBal);
                        ledgerCmd.Parameters.AddWithValue("@notes", $"Payment via {payment.PaymentMethod}" + (!string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? $" (Ref: {payment.ReferenceNumber})" : ""));
                        ledgerCmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return paymentId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public string GenerateNextPaymentNumber()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM supplier_payments;";
                int count = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                return $"PMT-{DateTime.Now.Year}-{count:D4}";
            }
        }

        public IEnumerable<SupplierLedgerEntry> GetLedger(long supplierId)
        {
            var list = new List<SupplierLedgerEntry>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT * FROM supplier_ledger
                    WHERE supplier_id = @supId
                    ORDER BY transaction_date DESC, id DESC;
                ";
                cmd.Parameters.AddWithValue("@supId", supplierId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new SupplierLedgerEntry
                        {
                            Id = Convert.ToInt64(r["id"]),
                            SupplierId = Convert.ToInt64(r["supplier_id"]),
                            TransactionType = r["transaction_type"].ToString(),
                            ReferenceId = r["reference_id"].ToString(),
                            Debit = Convert.ToDecimal(r["debit"]),
                            Credit = Convert.ToDecimal(r["credit"]),
                            BalanceAfter = Convert.ToDecimal(r["balance_after"]),
                            Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                            TransactionDate = Convert.ToDateTime(r["transaction_date"])
                        });
                    }
                }
            }
            return list;
        }

        private static SupplierPayment MapPayment(IDataRecord r)
        {
            return new SupplierPayment
            {
                Id = Convert.ToInt64(r["id"]),
                PaymentNumber = r["payment_number"].ToString(),
                SupplierId = Convert.ToInt64(r["supplier_id"]),
                SupplierName = r["supplier_name"] == DBNull.Value ? "" : r["supplier_name"].ToString(),
                PurchaseId = r["purchase_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["purchase_id"]),
                PurchaseNumber = r["purchase_number"] == DBNull.Value ? null : r["purchase_number"].ToString(),
                PaymentDate = Convert.ToDateTime(r["payment_date"]),
                PaymentMethod = r["payment_method"].ToString(),
                Amount = Convert.ToDecimal(r["amount"]),
                ReferenceNumber = r["reference_number"] == DBNull.Value ? null : r["reference_number"].ToString(),
                Notes = r["notes"] == DBNull.Value ? null : r["notes"].ToString(),
                CreatedAt = Convert.ToDateTime(r["created_at"])
            };
        }
    }

    public class StockMovementRepository : IStockMovementRepository
    {
        public IEnumerable<StockMovement> GetByProductId(long productId, int limit = 50)
        {
            var list = new List<StockMovement>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT sm.*, p.product_code, p.name_en AS product_name
                    FROM stock_movements sm
                    JOIN products p ON sm.product_id = p.id
                    WHERE sm.product_id = @prodId
                    ORDER BY sm.created_at DESC, sm.id DESC
                    LIMIT @limit;
                ";
                cmd.Parameters.AddWithValue("@prodId", productId);
                cmd.Parameters.AddWithValue("@limit", limit);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(MapMovement(r));
                }
            }
            return list;
        }

        public IEnumerable<StockMovement> GetByReference(string referenceType, string referenceId)
        {
            var list = new List<StockMovement>();
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT sm.*, p.product_code, p.name_en AS product_name
                    FROM stock_movements sm
                    JOIN products p ON sm.product_id = p.id
                    WHERE sm.reference_type = @refType AND sm.reference_id = @refId
                    ORDER BY sm.id ASC;
                ";
                cmd.Parameters.AddWithValue("@refType", referenceType);
                cmd.Parameters.AddWithValue("@refId", referenceId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(MapMovement(r));
                }
            }
            return list;
        }

        private static StockMovement MapMovement(IDataRecord r)
        {
            return new StockMovement
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
            };
        }
    }
}
