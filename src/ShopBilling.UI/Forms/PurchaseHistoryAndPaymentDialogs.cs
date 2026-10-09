using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.UI.Forms
{
    public class PurchaseHistoryControl : UserControl
    {
        private readonly PurchaseService _purchaseService;
        private readonly SupplierService _supplierService;

        private DataGridView gridHistory;
        private TextBox txtSearch;
        private ComboBox cmbSupplierFilter;
        private ComboBox cmbStatusFilter;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private Label lblStats;

        private PurchaseFilter currentFilter = new PurchaseFilter { PageNumber = 1, PageSize = 50 };

        public PurchaseHistoryControl(PurchaseService purchaseService, SupplierService supplierService)
        {
            _purchaseService = purchaseService;
            _supplierService = supplierService;

            InitializeUI();
            LoadFilterDropdowns();
            ExecuteSearch();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9f);
            this.Dock = DockStyle.Fill;

            // Filter bar
            var pnlFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var l1 = new Label { Text = "Search:", Location = new Point(10, 18), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            txtSearch = new TextBox { Location = new Point(60, 16), Width = 160 };
            txtSearch.TextChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var l2 = new Label { Text = "Supplier:", Location = new Point(230, 18), AutoSize = true };
            cmbSupplierFilter = new ComboBox { Location = new Point(285, 16), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSupplierFilter.SelectedIndexChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var l3 = new Label { Text = "Status:", Location = new Point(455, 18), AutoSize = true };
            cmbStatusFilter = new ComboBox { Location = new Point(500, 16), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatusFilter.Items.AddRange(new object[] { "All", "Draft", "Confirmed", "Cancelled" });
            cmbStatusFilter.SelectedIndex = 0;
            cmbStatusFilter.SelectedIndexChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var l4 = new Label { Text = "From:", Location = new Point(620, 18), AutoSize = true };
            dtpFrom = new DateTimePicker { Location = new Point(660, 16), Width = 100, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-30) };

            var l5 = new Label { Text = "To:", Location = new Point(770, 18), AutoSize = true };
            dtpTo = new DateTimePicker { Location = new Point(795, 16), Width = 100, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            var btnFilter = new Button { Text = "Apply", Location = new Point(905, 14), Size = new Size(65, 27), FlatStyle = FlatStyle.Flat };
            btnFilter.Click += (s, e) => ExecuteSearch();

            pnlFilters.Controls.AddRange(new Control[] { l1, txtSearch, l2, cmbSupplierFilter, l3, cmbStatusFilter, l4, dtpFrom, l5, dtpTo, btnFilter });

            // Grid
            gridHistory = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            gridHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 78, 121);
            gridHistory.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            gridHistory.ColumnHeadersHeight = 30;
            gridHistory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gridHistory.RowTemplate.Height = 26;

            gridHistory.Columns.Add("Num", "Purchase #");
            gridHistory.Columns.Add("Supplier", "Supplier Name");
            gridHistory.Columns.Add("Invoice", "Supplier Inv #");
            gridHistory.Columns.Add("Date", "Received Date");
            gridHistory.Columns.Add("TotalQty", "Qty");
            gridHistory.Columns.Add("GrandTotal", "Grand Total ₹");
            gridHistory.Columns.Add("Paid", "Paid ₹");
            gridHistory.Columns.Add("Due", "Balance Due ₹");
            gridHistory.Columns.Add("Status", "Status");

            // Actions panel at bottom
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.White, Padding = new Padding(12, 6, 12, 6) };
            lblStats = new Label { Text = "Ready.", Dock = DockStyle.Left, AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var btnPrintReceipt = new Button { Text = "Reprint Receipt", Dock = DockStyle.Right, Size = new Size(130, 32), FlatStyle = FlatStyle.Flat };
            btnPrintReceipt.Click += (s, e) => ReprintSelected();

            pnlBottom.Controls.Add(btnPrintReceipt);
            pnlBottom.Controls.Add(lblStats);

            this.Controls.Add(gridHistory);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlFilters);
        }

        private void LoadFilterDropdowns()
        {
            var sups = _supplierService.GetAllActive().ToList();
            sups.Insert(0, new Supplier { Id = 0, Name = "-- All Suppliers --" });
            cmbSupplierFilter.DataSource = sups;
            cmbSupplierFilter.DisplayMember = "Name";
            cmbSupplierFilter.ValueMember = "Id";
        }

        public void ExecuteSearch()
        {
            currentFilter.SearchTerm = txtSearch.Text;
            currentFilter.SupplierId = (cmbSupplierFilter.SelectedValue is long sId && sId > 0) ? sId : (long?)null;
            currentFilter.Status = cmbStatusFilter.SelectedItem?.ToString();
            currentFilter.FromDate = dtpFrom.Value;
            currentFilter.ToDate = dtpTo.Value;

            var res = _purchaseService.Search(currentFilter);
            gridHistory.Rows.Clear();

            foreach (var p in res.Items)
            {
                int rIdx = gridHistory.Rows.Add(
                    p.PurchaseNumber,
                    p.SupplierName,
                    p.SupplierInvoiceNumber ?? "-",
                    p.GoodsReceivedDate.ToString("dd-MMM-yyyy"),
                    p.TotalQty,
                    $"₹{p.GrandTotal:N2}",
                    $"₹{p.AmountPaid:N2}",
                    $"₹{p.BalanceDue:N2}",
                    p.Status
                );
                var row = gridHistory.Rows[rIdx];
                row.Tag = p;
                if (p.Status == "Confirmed") row.Cells["Status"].Style.ForeColor = Color.DarkGreen;
                else if (p.Status == "Cancelled") row.Cells["Status"].Style.ForeColor = Color.DarkRed;
            }

            lblStats.Text = $"Found {res.TotalCount} purchases | Execution: {res.ExecutionTimeMs:F1} ms";
        }

        private void ReprintSelected()
        {
            if (gridHistory.SelectedRows.Count == 0) return;
            var ph = gridHistory.SelectedRows[0].Tag as PurchaseHeader;
            if (ph != null)
            {
                var full = _purchaseService.GetById(ph.Id);
                using (var dlg = new PurchasePrintReceiptDialog(full))
                {
                    dlg.ShowDialog();
                }
            }
        }
    }

    public class SupplierPaymentDialog : Form
    {
        private readonly SupplierPaymentService _paymentService;
        private readonly SupplierService _supplierService;
        private readonly PurchaseHeader _linkedPurchase;

        private ComboBox cmbSupplier;
        private NumericUpDown numAmount;
        private ComboBox cmbMethod;
        private DateTimePicker dtpDate;
        private TextBox txtRef;
        private TextBox txtNotes;
        private Label lblOutstanding;

        public SupplierPaymentDialog(
            SupplierPaymentService paymentService,
            SupplierService supplierService,
            PurchaseHeader linkedPurchase = null)
        {
            _paymentService = paymentService;
            _supplierService = supplierService;
            _linkedPurchase = linkedPurchase;

            InitializeUI();
            LoadData();
        }

        private void InitializeUI()
        {
            this.Text = "Supplier Payment Entry";
            this.Size = new Size(460, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            var l1 = new Label { Text = "Supplier *:", Location = new Point(20, 25), AutoSize = true };
            cmbSupplier = new ComboBox { Location = new Point(130, 22), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSupplier.SelectedIndexChanged += (s, e) => UpdateSupplierBalanceLabel();

            lblOutstanding = new Label { Text = "Current Payable: ₹0.00", Location = new Point(130, 52), AutoSize = true, ForeColor = Color.DarkRed, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            var l2 = new Label { Text = "Payment Amount ₹ *:", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            numAmount = new NumericUpDown { Location = new Point(150, 82), Width = 150, DecimalPlaces = 2, Maximum = 9999999, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

            var l3 = new Label { Text = "Payment Method:", Location = new Point(20, 125), AutoSize = true };
            cmbMethod = new ComboBox { Location = new Point(150, 122), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.AddRange(new object[] { "Cash", "Bank Transfer", "UPI", "Cheque", "Other" });
            cmbMethod.SelectedIndex = 0;

            var l4 = new Label { Text = "Date:", Location = new Point(20, 160), AutoSize = true };
            dtpDate = new DateTimePicker { Location = new Point(150, 158), Width = 130, Format = DateTimePickerFormat.Short };

            var l5 = new Label { Text = "Reference / UTR #:", Location = new Point(20, 195), AutoSize = true };
            txtRef = new TextBox { Location = new Point(150, 192), Width = 260 };

            var l6 = new Label { Text = "Notes:", Location = new Point(20, 230), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(150, 228), Width = 260 };

            var btnSave = new Button { Text = "Record Payment", Location = new Point(170, 275), Size = new Size(130, 32), BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSave.Click += (s, e) => SavePayment();

            var btnCancel = new Button { Text = "Cancel", Location = new Point(310, 275), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            this.Controls.AddRange(new Control[] { l1, cmbSupplier, lblOutstanding, l2, numAmount, l3, cmbMethod, l4, dtpDate, l5, txtRef, l6, txtNotes, btnSave, btnCancel });
        }

        private void LoadData()
        {
            var sups = _supplierService.GetAllActive().ToList();
            cmbSupplier.DataSource = sups;
            cmbSupplier.DisplayMember = "Name";
            cmbSupplier.ValueMember = "Id";

            if (_linkedPurchase != null)
            {
                cmbSupplier.SelectedValue = _linkedPurchase.SupplierId;
                cmbSupplier.Enabled = false;
                numAmount.Value = _linkedPurchase.BalanceDue;
            }
            UpdateSupplierBalanceLabel();
        }

        private void UpdateSupplierBalanceLabel()
        {
            if (cmbSupplier.SelectedItem is Supplier s)
            {
                lblOutstanding.Text = $"Current Payable Balance: ₹{s.CurrentBalance:N2}";
            }
        }

        private void SavePayment()
        {
            if (numAmount.Value <= 0)
            {
                MessageBox.Show("Amount must be greater than zero.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var payment = new SupplierPayment
            {
                SupplierId = (long)cmbSupplier.SelectedValue,
                PurchaseId = _linkedPurchase?.Id,
                Amount = numAmount.Value,
                PaymentMethod = cmbMethod.SelectedItem.ToString(),
                PaymentDate = dtpDate.Value,
                ReferenceNumber = txtRef.Text.Trim(),
                Notes = txtNotes.Text.Trim()
            };

            var res = _paymentService.RecordPayment(payment);
            if (!res.IsValid)
            {
                MessageBox.Show(string.Join("\n", res.Errors), "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show($"Payment #{payment.PaymentNumber} recorded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    public class PurchasePrintReceiptDialog : Form
    {
        private readonly PurchaseHeader _purchase;
        private PrintDocument printDoc = new PrintDocument();
        private PrintPreviewControl printPreview;

        public PurchasePrintReceiptDialog(PurchaseHeader purchase)
        {
            _purchase = purchase;
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = $"Purchase Receipt Print Preview — {_purchase.PurchaseNumber}";
            this.Size = new Size(820, 680);
            this.StartPosition = FormStartPosition.CenterParent;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White, Padding = new Padding(10, 5, 10, 5) };
            var btnPrint = new Button { Text = "Print (Default Printer)", Size = new Size(160, 30), BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnPrint.Click += (s, e) =>
            {
                try
                {
                    printDoc.Print();
                    MessageBox.Show("Purchase document sent to printer.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            pnlTop.Controls.Add(btnPrint);

            printPreview = new PrintPreviewControl { Dock = DockStyle.Fill, Document = printDoc, Zoom = 1.0 };
            printDoc.PrintPage += PrintDocument_PrintPage;

            this.Controls.Add(printPreview);
            this.Controls.Add(pnlTop);
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            var fontTitle = new Font("Segoe UI", 14f, FontStyle.Bold);
            var fontHeader = new Font("Segoe UI", 9f, FontStyle.Bold);
            var fontRegular = new Font("Segoe UI", 8.5f);
            var brush = Brushes.Black;

            int y = 40;
            g.DrawString("SRI MURUGAN SUPER MARKET — GOODS INBOUND RECEIPT", fontTitle, brush, 40, y);
            y += 25;
            g.DrawString("124 Bazaar Road, T. Nagar, Chennai - 600017 | GSTIN: 33AAAAA0000A1Z5", fontRegular, brush, 40, y);
            y += 20;
            g.DrawLine(Pens.Black, 40, y, 760, y);
            y += 10;

            g.DrawString($"Purchase Order #: {_purchase.PurchaseNumber}", fontHeader, brush, 40, y);
            g.DrawString($"Date: {_purchase.GoodsReceivedDate:dd-MMM-yyyy}", fontRegular, brush, 550, y);
            y += 20;

            g.DrawString($"Supplier: {_purchase.SupplierName}", fontRegular, brush, 40, y);
            g.DrawString($"Supplier Inv #: {_purchase.SupplierInvoiceNumber ?? "N/A"}", fontRegular, brush, 550, y);
            y += 20;
            g.DrawString($"Status: {_purchase.Status.ToUpper()}", fontHeader, brush, 40, y);
            y += 25;

            // Table Header
            g.FillRectangle(new SolidBrush(Color.FromArgb(230, 230, 230)), 40, y, 720, 24);
            g.DrawRectangle(Pens.Gray, 40, y, 720, 24);
            g.DrawString("Item Description", fontHeader, brush, 45, y + 4);
            g.DrawString("Qty", fontHeader, brush, 360, y + 4);
            g.DrawString("Free", fontHeader, brush, 420, y + 4);
            g.DrawString("Rate ₹", fontHeader, brush, 480, y + 4);
            g.DrawString("Tax %", fontHeader, brush, 560, y + 4);
            g.DrawString("Total ₹", fontHeader, brush, 660, y + 4);
            y += 26;

            foreach (var it in _purchase.Items)
            {
                g.DrawString(it.ProductName, fontRegular, brush, 45, y);
                g.DrawString(it.Quantity.ToString("N0"), fontRegular, brush, 360, y);
                g.DrawString(it.FreeQuantity.ToString("N0"), fontRegular, brush, 420, y);
                g.DrawString($"₹{it.PurchaseRate:N2}", fontRegular, brush, 480, y);
                g.DrawString($"{it.TaxRate}%", fontRegular, brush, 560, y);
                g.DrawString($"₹{it.LineTotal:N2}", fontHeader, brush, 660, y);
                y += 20;
            }

            y += 10;
            g.DrawLine(Pens.Black, 40, y, 760, y);
            y += 10;

            g.DrawString($"Total Quantity Received: {_purchase.TotalQty + _purchase.TotalFreeQty:N0}", fontRegular, brush, 40, y);
            g.DrawString($"Grand Total: ₹{_purchase.GrandTotal:N2}", fontTitle, brush, 520, y);
            y += 25;
            g.DrawString($"Amount Paid: ₹{_purchase.AmountPaid:N2}", fontHeader, brush, 520, y);
            y += 20;
            g.DrawString($"Balance Due: ₹{_purchase.BalanceDue:N2}", fontHeader, Brushes.DarkRed, 520, y);
            y += 40;

            g.DrawString("Authorized Receiver Signature: _______________________", fontRegular, brush, 40, y);
        }
    }
}
