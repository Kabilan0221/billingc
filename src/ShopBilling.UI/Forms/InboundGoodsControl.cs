using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.UI.Forms
{
    public class InboundGoodsControl : UserControl
    {
        private readonly PurchaseService _purchaseService;
        private readonly SupplierService _supplierService;
        private readonly ProductService _productService;
        private readonly IAuditLogRepository _auditRepo;

        private PurchaseHeader currentPurchase = new PurchaseHeader();

        // Header controls
        private TextBox txtPurchaseNumber;
        private ComboBox cmbSupplier;
        private TextBox txtInvoiceNumber;
        private DateTimePicker dtpInvoiceDate;
        private DateTimePicker dtpReceivedDate;
        private ComboBox cmbPurchaseType;
        private DateTimePicker dtpDueDate;
        private TextBox txtNotes;
        private Label lblStatusBadge;

        // Product Selector
        private TextBox txtProductSearch;
        private ListBox lstProductSearchResults;
        private NumericUpDown numQty;
        private NumericUpDown numFreeQty;
        private NumericUpDown numRate;
        private NumericUpDown numDiscPct;
        private ComboBox cmbTaxRate;
        private TextBox txtBatch;
        private DateTimePicker dtpExpiry;
        private Button btnAddItem;

        // DataGridView
        private DataGridView gridItems;

        // Summary labels
        private Label lblTotalQty;
        private Label lblTotalFreeQty;
        private Label lblGrossAmount;
        private Label lblItemDiscount;
        private Label lblTaxableAmount;
        private Label lblTaxAmount;
        private Label lblRoundOff;
        private Label lblGrandTotal;
        private NumericUpDown numAmountPaid;
        private Label lblBalanceDue;

        // Actions
        private Button btnSaveDraft;
        private Button btnConfirm;
        private Button btnCancelPurchase;
        private Button btnPrint;
        private Button btnNewEntry;

        private Product selectedSearchProduct = null;

        public InboundGoodsControl(
            PurchaseService purchaseService,
            SupplierService supplierService,
            ProductService productService,
            IAuditLogRepository auditRepo)
        {
            _purchaseService = purchaseService;
            _supplierService = supplierService;
            _productService = productService;
            _auditRepo = auditRepo;

            InitializeUI();
            LoadSuppliers();
            ResetForm();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9f);
            this.Dock = DockStyle.Fill;

            // 1. Top Status & Workflow Action Strip
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            btnNewEntry = CreateBtn("New Entry", Color.FromArgb(52, 73, 94), (s, e) => ResetForm());
            btnSaveDraft = CreateBtn("Save as Draft", Color.FromArgb(41, 128, 185), (s, e) => SaveDraft());
            btnConfirm = CreateBtn("✔ Confirm Purchase & Update Stock", Color.FromArgb(39, 174, 96), (s, e) => ConfirmPurchase());
            btnCancelPurchase = CreateBtn("Cancel / Reverse", Color.FromArgb(192, 57, 43), (s, e) => CancelPurchase());
            btnPrint = CreateBtn("Print Receipt", Color.FromArgb(142, 68, 173), (s, e) => PrintReceipt());

            lblStatusBadge = new Label
            {
                Text = "STATUS: DRAFT",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 128, 185),
                AutoSize = false,
                Size = new Size(140, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right
            };

            pnlTop.Controls.AddRange(new Control[] { btnNewEntry, btnSaveDraft, btnConfirm, btnCancelPurchase, btnPrint, lblStatusBadge });
            int curX = 12;
            foreach (Control c in new Control[] { btnNewEntry, btnSaveDraft, btnConfirm, btnCancelPurchase, btnPrint })
            {
                c.Location = new Point(curX, 8);
                curX += c.Width + 6;
            }

            // 2. Purchase Header Details GroupBox
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = Color.White,
                Padding = new Padding(12, 6, 12, 6)
            };

            var grpHeader = new GroupBox
            {
                Text = "Purchase Inbound Header Details",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            var l1 = new Label { Text = "Purchase #:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            txtPurchaseNumber = new TextBox { Location = new Point(85, 20), Width = 130, ReadOnly = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            var l2 = new Label { Text = "Supplier *:", Location = new Point(230, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            cmbSupplier = new ComboBox { Location = new Point(295, 20), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f) };

            var l3 = new Label { Text = "Supplier Inv #:", Location = new Point(540, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            txtInvoiceNumber = new TextBox { Location = new Point(630, 20), Width = 130, Font = new Font("Segoe UI", 8.5f) };

            var l4 = new Label { Text = "Inv Date:", Location = new Point(780, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            dtpInvoiceDate = new DateTimePicker { Location = new Point(840, 20), Width = 110, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 8.5f) };

            var l5 = new Label { Text = "Received:", Location = new Point(12, 54), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            dtpReceivedDate = new DateTimePicker { Location = new Point(85, 52), Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 8.5f) };

            var l6 = new Label { Text = "Type:", Location = new Point(230, 54), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            cmbPurchaseType = new ComboBox { Location = new Point(295, 52), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f) };
            cmbPurchaseType.Items.AddRange(new object[] { "Credit", "Cash" });
            cmbPurchaseType.SelectedIndex = 0;

            var l7 = new Label { Text = "Due Date:", Location = new Point(420, 54), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            dtpDueDate = new DateTimePicker { Location = new Point(485, 52), Width = 110, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 8.5f) };

            var l8 = new Label { Text = "Notes:", Location = new Point(610, 54), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            txtNotes = new TextBox { Location = new Point(655, 52), Width = 295, Font = new Font("Segoe UI", 8.5f) };

            grpHeader.Controls.AddRange(new Control[] { l1, txtPurchaseNumber, l2, cmbSupplier, l3, txtInvoiceNumber, l4, dtpInvoiceDate, l5, dtpReceivedDate, l6, cmbPurchaseType, l7, dtpDueDate, l8, txtNotes });
            pnlHeader.Controls.Add(grpHeader);

            // 3. Product Search & Add Strip (Searches 50,000 items)
            var pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 6, 12, 6)
            };

            var grpSearch = new GroupBox
            {
                Text = "Search & Add Product to Inbound List (Indexed lookup across 50,000 items)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            var ls1 = new Label { Text = "Find Product:", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            txtProductSearch = new TextBox { Location = new Point(90, 22), Width = 220, Font = new Font("Segoe UI", 9f) };
            txtProductSearch.TextChanged += (s, e) => OnProductSearchTextChanged();

            lstProductSearchResults = new ListBox { Location = new Point(90, 48), Size = new Size(300, 140), Visible = false, Font = new Font("Segoe UI", 8.5f) };
            lstProductSearchResults.DoubleClick += (s, e) => SelectSearchProduct();
            this.Controls.Add(lstProductSearchResults); // Bring to top

            var ls2 = new Label { Text = "Qty *:", Location = new Point(320, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            numQty = new NumericUpDown { Location = new Point(360, 22), Width = 65, DecimalPlaces = 0, Maximum = 999999, Value = 1 };

            var ls3 = new Label { Text = "Free:", Location = new Point(435, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            numFreeQty = new NumericUpDown { Location = new Point(470, 22), Width = 60, DecimalPlaces = 0, Maximum = 999999, Value = 0 };

            var ls4 = new Label { Text = "Rate ₹:", Location = new Point(540, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            numRate = new NumericUpDown { Location = new Point(590, 22), Width = 80, DecimalPlaces = 2, Maximum = 9999999 };

            var ls5 = new Label { Text = "Disc %:", Location = new Point(680, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            numDiscPct = new NumericUpDown { Location = new Point(730, 22), Width = 55, DecimalPlaces = 2, Maximum = 100 };

            var ls6 = new Label { Text = "Tax %:", Location = new Point(795, 24), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            cmbTaxRate = new ComboBox { Location = new Point(840, 22), Width = 65, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTaxRate.Items.AddRange(new object[] { "0", "5", "12", "18", "28" });
            cmbTaxRate.SelectedItem = "5";

            btnAddItem = new Button { Text = "+ Add Item", Location = new Point(915, 20), Size = new Size(85, 26), BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnAddItem.Click += (s, e) => AddItemToGrid();

            grpSearch.Controls.AddRange(new Control[] { ls1, txtProductSearch, ls2, numQty, ls3, numFreeQty, ls4, numRate, ls5, numDiscPct, ls6, cmbTaxRate, btnAddItem });
            pnlSearch.Controls.Add(grpSearch);

            // 4. Items DataGridView
            gridItems = new DataGridView
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

            gridItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 78, 121);
            gridItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            gridItems.ColumnHeadersHeight = 30;
            gridItems.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gridItems.RowTemplate.Height = 26;

            ConfigureGridColumns();

            // 5. Bottom Calculation & Totals Summary Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                BackColor = Color.FromArgb(235, 241, 248),
                Padding = new Padding(12, 6, 12, 6),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Line 1: Quantities and Gross
            lblTotalQty = new Label { Text = "Purchased Qty: 0", Location = new Point(14, 10), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblTotalFreeQty = new Label { Text = "Free Qty: 0", Location = new Point(150, 10), AutoSize = true, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblGrossAmount = new Label { Text = "Gross: ₹0.00", Location = new Point(270, 10), AutoSize = true, Font = new Font("Segoe UI", 9f) };
            lblItemDiscount = new Label { Text = "Discount: ₹0.00", Location = new Point(410, 10), AutoSize = true, Font = new Font("Segoe UI", 9f) };
            lblTaxableAmount = new Label { Text = "Taxable: ₹0.00", Location = new Point(560, 10), AutoSize = true, Font = new Font("Segoe UI", 9f) };
            lblTaxAmount = new Label { Text = "Tax: ₹0.00", Location = new Point(710, 10), AutoSize = true, Font = new Font("Segoe UI", 9f) };

            // Line 2: Grand Total, Paid, Balance Due
            lblRoundOff = new Label { Text = "Round: ₹0.00", Location = new Point(14, 45), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.Gray };
            lblGrandTotal = new Label { Text = "GRAND TOTAL: ₹0.00", Location = new Point(160, 40), AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 110, 190) };

            var lp = new Label { Text = "Amount Paid ₹:", Location = new Point(490, 45), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            numAmountPaid = new NumericUpDown { Location = new Point(590, 42), Width = 110, DecimalPlaces = 2, Maximum = 9999999, Font = new Font("Segoe UI", 9.5f) };
            numAmountPaid.ValueChanged += (s, e) => RecalculateTotals();

            lblBalanceDue = new Label { Text = "Balance Due: ₹0.00", Location = new Point(730, 44), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.DarkRed };

            pnlBottom.Controls.AddRange(new Control[] {
                lblTotalQty, lblTotalFreeQty, lblGrossAmount, lblItemDiscount, lblTaxableAmount, lblTaxAmount,
                lblRoundOff, lblGrandTotal, lp, numAmountPaid, lblBalanceDue
            });

            this.Controls.Add(gridItems);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlSearch);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlTop);
        }

        private Button CreateBtn(string text, Color bg, EventHandler click)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Size = new Size(130, 32),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += click;
            return btn;
        }

        private void ConfigureGridColumns()
        {
            gridItems.Columns.Clear();
            gridItems.Columns.Add("Code", "Code");
            gridItems.Columns["Code"].FillWeight = 50;

            gridItems.Columns.Add("Barcode", "Barcode");
            gridItems.Columns["Barcode"].FillWeight = 75;

            gridItems.Columns.Add("Name", "Product Name");
            gridItems.Columns["Name"].FillWeight = 160;

            gridItems.Columns.Add("Qty", "Qty");
            gridItems.Columns["Qty"].FillWeight = 40;
            gridItems.Columns["Qty"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridItems.Columns.Add("Free", "Free");
            gridItems.Columns["Free"].FillWeight = 35;
            gridItems.Columns["Free"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridItems.Columns.Add("Rate", "Rate ₹");
            gridItems.Columns["Rate"].FillWeight = 50;
            gridItems.Columns["Rate"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridItems.Columns.Add("Disc", "Disc ₹");
            gridItems.Columns["Disc"].FillWeight = 45;
            gridItems.Columns["Disc"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridItems.Columns.Add("TaxRate", "Tax %");
            gridItems.Columns["TaxRate"].FillWeight = 40;
            gridItems.Columns["TaxRate"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            gridItems.Columns.Add("TaxAmt", "Tax ₹");
            gridItems.Columns["TaxAmt"].FillWeight = 45;
            gridItems.Columns["TaxAmt"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridItems.Columns.Add("Total", "Line Total ₹");
            gridItems.Columns["Total"].FillWeight = 60;
            gridItems.Columns["Total"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            gridItems.Columns["Total"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            var btnCol = new DataGridViewButtonColumn();
            btnCol.Name = "Action";
            btnCol.HeaderText = "";
            btnCol.Text = "✕";
            btnCol.UseColumnTextForButtonValue = true;
            btnCol.FillWeight = 25;
            gridItems.Columns.Add(btnCol);

            gridItems.CellContentClick += (s, e) =>
            {
                if (e.ColumnIndex == gridItems.Columns["Action"].Index && e.RowIndex >= 0)
                {
                    if (currentPurchase.Status != "Draft")
                    {
                        MessageBox.Show("Items cannot be removed from confirmed or cancelled purchases.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    currentPurchase.Items.RemoveAt(e.RowIndex);
                    RefreshGrid();
                }
            };
        }

        private void LoadSuppliers()
        {
            var sups = _supplierService.GetAllActive().ToList();
            cmbSupplier.DataSource = sups;
            cmbSupplier.DisplayMember = "Name";
            cmbSupplier.ValueMember = "Id";
        }

        private void ResetForm()
        {
            currentPurchase = new PurchaseHeader
            {
                PurchaseNumber = _purchaseService.GetById(0)?.PurchaseNumber ?? "PUR-" + DateTime.Now.Year + "-" + DateTime.Now.ToString("HHmmss"),
                Status = "Draft",
                GoodsReceivedDate = DateTime.Now,
                Items = new List<PurchaseItem>()
            };

            txtPurchaseNumber.Text = currentPurchase.PurchaseNumber;
            if (cmbSupplier.Items.Count > 0) cmbSupplier.SelectedIndex = 0;
            txtInvoiceNumber.Text = "";
            dtpInvoiceDate.Value = DateTime.Now;
            dtpReceivedDate.Value = DateTime.Now;
            cmbPurchaseType.SelectedIndex = 0;
            dtpDueDate.Value = DateTime.Now.AddDays(30);
            txtNotes.Text = "";
            numAmountPaid.Value = 0;

            UpdateStatusBadge();
            RefreshGrid();
        }

        private void UpdateStatusBadge()
        {
            lblStatusBadge.Text = $"STATUS: {currentPurchase.Status.ToUpper()}";
            if (currentPurchase.Status == "Draft")
            {
                lblStatusBadge.ForeColor = Color.FromArgb(41, 128, 185);
                btnConfirm.Enabled = true;
                btnCancelPurchase.Enabled = false;
                btnSaveDraft.Enabled = true;
            }
            else if (currentPurchase.Status == "Confirmed")
            {
                lblStatusBadge.ForeColor = Color.FromArgb(39, 174, 96);
                btnConfirm.Enabled = false;
                btnCancelPurchase.Enabled = true;
                btnSaveDraft.Enabled = false;
            }
            else
            {
                lblStatusBadge.ForeColor = Color.FromArgb(192, 57, 43);
                btnConfirm.Enabled = false;
                btnCancelPurchase.Enabled = false;
                btnSaveDraft.Enabled = false;
            }
        }

        private void OnProductSearchTextChanged()
        {
            string q = txtProductSearch.Text.Trim();
            if (q.Length < 2)
            {
                lstProductSearchResults.Visible = false;
                return;
            }

            // High performance indexed lookup on 50,000 items
            var filter = new ProductFilter { SearchTerm = q, PageNumber = 1, PageSize = 10 };
            var res = _productService.GetProducts(filter);

            lstProductSearchResults.Items.Clear();
            foreach (var p in res.Items)
            {
                lstProductSearchResults.Items.Add(new ProductSearchItem(p));
            }

            lstProductSearchResults.Visible = lstProductSearchResults.Items.Count > 0;
            lstProductSearchResults.BringToFront();
        }

        private void SelectSearchProduct()
        {
            if (lstProductSearchResults.SelectedItem is ProductSearchItem item)
            {
                selectedSearchProduct = item.Product;
                txtProductSearch.Text = item.Product.NameEn;
                numRate.Value = item.Product.PurchaseRate;
                cmbTaxRate.SelectedItem = item.Product.TaxRate.ToString("0");
                lstProductSearchResults.Visible = false;
                numQty.Focus();
            }
        }

        private void AddItemToGrid()
        {
            if (selectedSearchProduct == null)
            {
                MessageBox.Show("Please select a product from search results first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (numQty.Value <= 0)
            {
                MessageBox.Show("Quantity must be greater than zero.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = new PurchaseItem
            {
                ProductId = selectedSearchProduct.Id,
                ProductCode = selectedSearchProduct.ProductCode,
                Barcode = selectedSearchProduct.Barcode,
                ProductName = selectedSearchProduct.NameEn,
                Quantity = numQty.Value,
                FreeQuantity = numFreeQty.Value,
                PurchaseRate = numRate.Value,
                DiscountPercent = numDiscPct.Value,
                TaxRate = decimal.Parse(cmbTaxRate.SelectedItem.ToString())
            };

            currentPurchase.Items.Add(item);
            txtProductSearch.Text = "";
            selectedSearchProduct = null;
            numQty.Value = 1;
            numFreeQty.Value = 0;

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            PurchaseService.CalculatePurchase(currentPurchase);

            gridItems.Rows.Clear();
            foreach (var it in currentPurchase.Items)
            {
                gridItems.Rows.Add(
                    it.ProductCode,
                    it.Barcode,
                    it.ProductName,
                    it.Quantity,
                    it.FreeQuantity,
                    $"₹{it.PurchaseRate:N2}",
                    $"₹{it.DiscountAmount:N2}",
                    $"{it.TaxRate}%",
                    $"₹{it.TaxAmount:N2}",
                    $"₹{it.LineTotal:N2}"
                );
            }

            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            currentPurchase.AmountPaid = numAmountPaid.Value;
            PurchaseService.CalculatePurchase(currentPurchase);

            lblTotalQty.Text = $"Purchased Qty: {currentPurchase.TotalQty:N0}";
            lblTotalFreeQty.Text = $"Free Qty: {currentPurchase.TotalFreeQty:N0}";
            lblGrossAmount.Text = $"Gross: ₹{currentPurchase.GrossAmount:N2}";
            lblItemDiscount.Text = $"Discount: ₹{currentPurchase.ItemDiscount:N2}";
            lblTaxableAmount.Text = $"Taxable: ₹{currentPurchase.TaxableAmount:N2}";
            lblTaxAmount.Text = $"Tax: ₹{currentPurchase.TaxAmount:N2}";
            lblRoundOff.Text = $"Round: ₹{currentPurchase.RoundOff:N2}";
            lblGrandTotal.Text = $"GRAND TOTAL: ₹{currentPurchase.GrandTotal:N2}";
            lblBalanceDue.Text = $"Balance Due: ₹{currentPurchase.BalanceDue:N2}";
        }

        private void SaveDraft()
        {
            SyncHeaderFields();
            var res = _purchaseService.SaveDraft(currentPurchase);
            if (!res.IsValid)
            {
                MessageBox.Show(string.Join("\n", res.Errors), "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show($"Purchase Draft #{currentPurchase.PurchaseNumber} saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateStatusBadge();
        }

        private void ConfirmPurchase()
        {
            SyncHeaderFields();
            if (currentPurchase.Items.Count == 0)
            {
                MessageBox.Show("Please add at least one product item to confirm purchase.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save draft first if not yet in database
            if (currentPurchase.Id == 0)
            {
                var saveRes = _purchaseService.SaveDraft(currentPurchase);
                if (!saveRes.IsValid)
                {
                    MessageBox.Show(string.Join("\n", saveRes.Errors), "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var dialogRes = MessageBox.Show(
                $"Are you sure you want to CONFIRM Purchase #{currentPurchase.PurchaseNumber} for ₹{currentPurchase.GrandTotal:N2}?\n\nThis will atomically update inventory stock and supplier balance.",
                "Confirm Purchase", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dialogRes == DialogResult.Yes)
            {
                string errMsg;
                bool ok = _purchaseService.ConfirmPurchase(currentPurchase.Id, out errMsg);
                if (ok)
                {
                    currentPurchase.Status = "Confirmed";
                    UpdateStatusBadge();
                    MessageBox.Show($"Purchase #{currentPurchase.PurchaseNumber} successfully CONFIRMED!\nStock and Supplier ledger updated.", "Purchase Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Confirmation failed: {errMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CancelPurchase()
        {
            if (currentPurchase.Status != "Confirmed") return;

            var dialogRes = MessageBox.Show(
                $"Are you sure you want to REVERSE and CANCEL Purchase #{currentPurchase.PurchaseNumber}?\n\nStock will be safely deducted, and supplier payable reversed.",
                "Cancel Purchase Reversal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (dialogRes == DialogResult.Yes)
            {
                string errMsg;
                bool ok = _purchaseService.CancelPurchase(currentPurchase.Id, out errMsg);
                if (ok)
                {
                    currentPurchase.Status = "Cancelled";
                    UpdateStatusBadge();
                    MessageBox.Show($"Purchase #{currentPurchase.PurchaseNumber} reversed and cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Cancellation rejected: {errMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PrintReceipt()
        {
            if (currentPurchase.Items.Count == 0) return;
            SyncHeaderFields();
            using (var dlg = new PurchasePrintReceiptDialog(currentPurchase))
            {
                dlg.ShowDialog();
            }
        }

        private void SyncHeaderFields()
        {
            currentPurchase.PurchaseNumber = txtPurchaseNumber.Text.Trim();
            currentPurchase.SupplierId = (cmbSupplier.SelectedValue is long sId) ? sId : 0;
            currentPurchase.SupplierName = cmbSupplier.Text;
            currentPurchase.SupplierInvoiceNumber = txtInvoiceNumber.Text.Trim();
            currentPurchase.SupplierInvoiceDate = dtpInvoiceDate.Value;
            currentPurchase.GoodsReceivedDate = dtpReceivedDate.Value;
            currentPurchase.PurchaseType = cmbPurchaseType.SelectedItem?.ToString() ?? "Credit";
            currentPurchase.PaymentDueDate = dtpDueDate.Value;
            currentPurchase.Notes = txtNotes.Text.Trim();
            currentPurchase.AmountPaid = numAmountPaid.Value;
        }

        private class ProductSearchItem
        {
            public Product Product { get; }
            public ProductSearchItem(Product p) => Product = p;
            public override string ToString() => $"{Product.ProductCode} | {Product.NameEn} | Barcode: {Product.Barcode} | Rate: ₹{Product.PurchaseRate}";
        }
    }
}
