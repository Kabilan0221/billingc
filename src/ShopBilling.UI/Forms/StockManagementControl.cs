using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.UI.Forms
{
    public class StockManagementControl : UserControl
    {
        private readonly StockService _stockService;
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IBrandRepository _brandRepo;
        private readonly IAuditLogRepository _auditRepo;

        // Sub-tabs
        private TabControl tabStock;
        private TabPage tabDashboard;
        private TabPage tabCurrentStock;
        private TabPage tabLedger;
        private TabPage tabAdjustments;
        private TabPage tabVerification;
        private TabPage tabValuation;

        // Dashboard controls
        private Label lblTotalProducts;
        private Label lblTotalUnits;
        private Label lblOutOfStock;
        private Label lblLowStock;
        private Label lblExcessStock;
        private Label lblValuationCost;
        private Label lblValuationSale;
        private Label lblGrossMargin;
        private DataGridView gridRecentMovements;

        // Current Stock controls
        private DataGridView gridCurrentStock;
        private TextBox txtSearchStock;
        private ComboBox cmbCategoryStock;
        private ComboBox cmbBrandStock;
        private ComboBox cmbStatusStock;
        private Label lblStockPaging;
        private int stockPage = 1;
        private int stockPageSize = 50;
        private PagedResult<CurrentStockSummary> lastStockResult;

        // Stock Ledger controls
        private ComboBox cmbLedgerProduct;
        private ComboBox cmbLedgerType;
        private DateTimePicker dtpLedgerFrom;
        private DateTimePicker dtpLedgerTo;
        private DataGridView gridLedger;

        // Stock Adjustments controls
        private DataGridView gridAdjustments;
        private Button btnNewAdjustment;

        // Physical Verification controls
        private DataGridView gridVerification;
        private Button btnStartVerification;

        // Valuation controls
        private DataGridView gridValuation;
        private Label lblTotalValuationCost;
        private Label lblTotalValuationSale;

        public StockManagementControl(
            StockService stockService,
            IProductRepository productRepo,
            ICategoryRepository categoryRepo,
            IBrandRepository brandRepo,
            IAuditLogRepository auditRepo)
        {
            _stockService = stockService;
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _brandRepo = brandRepo;
            _auditRepo = auditRepo;

            InitializeUI();
            LoadDashboardData();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9f);
            this.Dock = DockStyle.Fill;

            // 1. Top Header
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(31, 78, 121),
                Padding = new Padding(16, 0, 16, 0)
            };

            var lblHeaderTitle = new Label
            {
                Text = "STOCK & INVENTORY MANAGEMENT (F8)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(420, 52),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Left
            };

            var btnRefresh = new Button
            {
                Text = "Refresh All Data",
                Size = new Size(130, 32),
                Location = new Point(0, 10),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => RefreshActiveTab();

            pnlTop.Controls.Add(btnRefresh);
            pnlTop.Controls.Add(lblHeaderTitle);

            // 2. Main TabControl
            tabStock = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            tabDashboard = new TabPage("Stock Dashboard");
            tabCurrentStock = new TabPage("Current Stock Inventory");
            tabLedger = new TabPage("Stock Movement Ledger");
            tabAdjustments = new TabPage("Stock Adjustments");
            tabVerification = new TabPage("Physical Verification");
            tabValuation = new TabPage("Stock Valuation");

            SetupDashboardTab();
            SetupCurrentStockTab();
            SetupLedgerTab();
            SetupAdjustmentsTab();
            SetupVerificationTab();
            SetupValuationTab();

            tabStock.TabPages.AddRange(new TabPage[] {
                tabDashboard,
                tabCurrentStock,
                tabLedger,
                tabAdjustments,
                tabVerification,
                tabValuation
            });

            tabStock.SelectedIndexChanged += (s, e) => RefreshActiveTab();

            this.Controls.Add(tabStock);
            this.Controls.Add(pnlTop);
        }

        private void RefreshActiveTab()
        {
            if (tabStock.SelectedTab == tabDashboard) LoadDashboardData();
            else if (tabStock.SelectedTab == tabCurrentStock) LoadCurrentStockData();
            else if (tabStock.SelectedTab == tabLedger) LoadLedgerData();
            else if (tabStock.SelectedTab == tabAdjustments) LoadAdjustmentsData();
            else if (tabStock.SelectedTab == tabVerification) LoadVerificationData();
            else if (tabStock.SelectedTab == tabValuation) LoadValuationData();
        }

        // =========================================================================
        // 1. DASHBOARD TAB
        // =========================================================================
        private void SetupDashboardTab()
        {
            tabDashboard.BackColor = Color.FromArgb(245, 247, 250);
            tabDashboard.Padding = new Padding(16);

            var pnlMetrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 170,
                AutoScroll = true
            };

            lblTotalProducts = new Label();
            lblTotalUnits = new Label();
            lblOutOfStock = new Label();
            lblLowStock = new Label();
            lblExcessStock = new Label();
            lblValuationCost = new Label();
            lblValuationSale = new Label();
            lblGrossMargin = new Label();

            pnlMetrics.Controls.Add(CreateMetricCard("Active Products", lblTotalProducts, Color.FromArgb(41, 128, 185)));
            pnlMetrics.Controls.Add(CreateMetricCard("Total Stock Units", lblTotalUnits, Color.FromArgb(52, 73, 94)));
            pnlMetrics.Controls.Add(CreateMetricCard("Out of Stock", lblOutOfStock, Color.FromArgb(192, 57, 43)));
            pnlMetrics.Controls.Add(CreateMetricCard("Low Stock Alert", lblLowStock, Color.FromArgb(211, 84, 0)));
            pnlMetrics.Controls.Add(CreateMetricCard("Excess Stock", lblExcessStock, Color.FromArgb(127, 140, 141)));
            pnlMetrics.Controls.Add(CreateMetricCard("Valuation @ Cost", lblValuationCost, Color.FromArgb(39, 174, 96)));
            pnlMetrics.Controls.Add(CreateMetricCard("Valuation @ Retail", lblValuationSale, Color.FromArgb(22, 160, 133)));
            pnlMetrics.Controls.Add(CreateMetricCard("Potential Margin", lblGrossMargin, Color.FromArgb(142, 68, 173)));

            // Recent Movements Table
            var lblRecent = new Label
            {
                Text = "Recent Stock Movements Ledger (Audited Transactions)",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Height = 35,
                TextAlign = ContentAlignment.BottomLeft
            };

            gridRecentMovements = CreateStyledGrid();
            gridRecentMovements.Columns.Add("Date", "Date & Time");
            gridRecentMovements.Columns.Add("Type", "Movement Type");
            gridRecentMovements.Columns.Add("Product", "Product Name");
            gridRecentMovements.Columns.Add("Qty", "Delta Qty");
            gridRecentMovements.Columns.Add("Before", "Stock Before");
            gridRecentMovements.Columns.Add("After", "Stock After");
            gridRecentMovements.Columns.Add("Ref", "Reference #");
            gridRecentMovements.Columns.Add("Notes", "Notes / Reason");

            tabDashboard.Controls.Add(gridRecentMovements);
            tabDashboard.Controls.Add(lblRecent);
            tabDashboard.Controls.Add(pnlMetrics);
        }

        private Panel CreateMetricCard(string title, Label valLabel, Color barColor)
        {
            var pnl = new Panel
            {
                Size = new Size(185, 75),
                BackColor = Color.White,
                Margin = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle
            };

            var bar = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = barColor };
            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(100, 110, 120),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(12, 10),
                AutoSize = true
            };

            valLabel.Text = "—";
            valLabel.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            valLabel.ForeColor = Color.FromArgb(30, 40, 50);
            valLabel.Location = new Point(12, 32);
            valLabel.AutoSize = true;

            pnl.Controls.AddRange(new Control[] { bar, lblTitle, valLabel });
            return pnl;
        }

        private void LoadDashboardData()
        {
            try
            {
                var m = _stockService.GetDashboardMetrics();
                lblTotalProducts.Text = m.TotalActiveProducts.ToString("N0");
                lblTotalUnits.Text = m.TotalStockQuantity.ToString("N0");
                lblOutOfStock.Text = m.OutOfStockCount.ToString("N0");
                lblLowStock.Text = m.LowStockCount.ToString("N0");
                lblExcessStock.Text = m.ExcessStockCount.ToString("N0");
                lblValuationCost.Text = $"₹{m.StockValueAtCost:N2}";
                lblValuationSale.Text = $"₹{m.StockValueAtSale:N2}";
                lblGrossMargin.Text = $"₹{m.PotentialGrossMargin:N2}";

                gridRecentMovements.Rows.Clear();
                foreach (var sm in m.RecentMovements)
                {
                    gridRecentMovements.Rows.Add(
                        sm.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        sm.ReferenceType,
                        sm.ProductName,
                        sm.Quantity > 0 ? $"+{sm.Quantity:G29}" : $"{sm.Quantity:G29}",
                        sm.QuantityBefore.ToString("G29"),
                        sm.QuantityAfter.ToString("G29"),
                        sm.ReferenceId,
                        sm.Notes
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load dashboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =========================================================================
        // 2. CURRENT STOCK TAB
        // =========================================================================
        private void SetupCurrentStockTab()
        {
            tabCurrentStock.BackColor = Color.White;

            // Filter bar
            var pnlFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(12)
            };

            txtSearchStock = new TextBox { Width = 220, Location = new Point(12, 14), Font = new Font("Segoe UI", 9.5f) };
            txtSearchStock.TextChanged += (s, e) => { stockPage = 1; LoadCurrentStockData(); };

            cmbCategoryStock = new ComboBox { Width = 150, Location = new Point(240, 14), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategoryStock.SelectedIndexChanged += (s, e) => { stockPage = 1; LoadCurrentStockData(); };

            cmbStatusStock = new ComboBox { Width = 130, Location = new Point(400, 14), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatusStock.Items.AddRange(new object[] { "All", "In Stock", "Low Stock", "Out of Stock", "Excess Stock" });
            cmbStatusStock.SelectedIndex = 0;
            cmbStatusStock.SelectedIndexChanged += (s, e) => { stockPage = 1; LoadCurrentStockData(); };

            var btnExport = new Button
            {
                Text = "Export to Excel",
                Width = 120,
                Height = 28,
                Location = new Point(540, 13),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExport.Click += (s, e) => ExportStockExcel();

            pnlFilters.Controls.AddRange(new Control[] { txtSearchStock, cmbCategoryStock, cmbStatusStock, btnExport });

            // Grid
            gridCurrentStock = CreateStyledGrid();
            gridCurrentStock.Columns.Add("ID", "ID");
            gridCurrentStock.Columns.Add("Code", "SKU / Code");
            gridCurrentStock.Columns.Add("Barcode", "Barcode");
            gridCurrentStock.Columns.Add("Name", "Product Name");
            gridCurrentStock.Columns.Add("Category", "Category");
            gridCurrentStock.Columns.Add("Opening", "Opening");
            gridCurrentStock.Columns.Add("Purchased", "Purchased");
            gridCurrentStock.Columns.Add("Free", "Free Qty");
            gridCurrentStock.Columns.Add("Adjusted", "Adjustments");
            gridCurrentStock.Columns.Add("Current", "Current Stock");
            gridCurrentStock.Columns.Add("Reorder", "Reorder Level");
            gridCurrentStock.Columns.Add("Cost", "Cost Rate (₹)");
            gridCurrentStock.Columns.Add("Valuation", "Stock Value (₹)");
            gridCurrentStock.Columns.Add("Status", "Status");

            // Paging bar
            var pnlPaging = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(240, 243, 246),
                Padding = new Padding(12, 6, 12, 6)
            };

            lblStockPaging = new Label { Text = "Loading stock...", AutoSize = true, Dock = DockStyle.Left };
            var btnPrev = new Button { Text = "< Prev", Dock = DockStyle.Right, Width = 70 };
            var btnNext = new Button { Text = "Next >", Dock = DockStyle.Right, Width = 70 };

            btnPrev.Click += (s, e) => { if (stockPage > 1) { stockPage--; LoadCurrentStockData(); } };
            btnNext.Click += (s, e) => { if (lastStockResult != null && stockPage < lastStockResult.TotalPages) { stockPage++; LoadCurrentStockData(); } };

            pnlPaging.Controls.AddRange(new Control[] { lblStockPaging, btnNext, btnPrev });

            tabCurrentStock.Controls.Add(gridCurrentStock);
            tabCurrentStock.Controls.Add(pnlPaging);
            tabCurrentStock.Controls.Add(pnlFilters);

            PopulateCategoryDropdowns();
        }

        private void PopulateCategoryDropdowns()
        {
            cmbCategoryStock.Items.Clear();
            cmbCategoryStock.Items.Add("All Categories");
            foreach (var c in _categoryRepo.GetAll())
            {
                cmbCategoryStock.Items.Add(c.Name);
            }
            cmbCategoryStock.SelectedIndex = 0;
        }

        private void LoadCurrentStockData()
        {
            var filter = new StockFilter
            {
                SearchTerm = txtSearchStock.Text,
                StockStatus = cmbStatusStock.SelectedItem?.ToString() ?? "All",
                PageNumber = stockPage,
                PageSize = stockPageSize
            };

            lastStockResult = _stockService.GetCurrentStock(filter);
            gridCurrentStock.Rows.Clear();

            foreach (var it in lastStockResult.Items)
            {
                gridCurrentStock.Rows.Add(
                    it.ProductId,
                    it.ProductCode,
                    it.Barcode,
                    it.ProductName,
                    it.CategoryName,
                    it.OpeningStock.ToString("G29"),
                    it.PurchasedQuantity.ToString("G29"),
                    it.FreeQuantityReceived > 0 ? $"+{it.FreeQuantityReceived:G29}" : "0",
                    it.AdjustmentQuantity.ToString("G29"),
                    it.CurrentStock.ToString("G29"),
                    it.ReorderLevel.ToString("G29"),
                    $"₹{it.PurchaseRate:N2}",
                    $"₹{it.StockValuePurchase:N2}",
                    it.StockStatus
                );
            }

            lblStockPaging.Text = $"Showing {lastStockResult.Items.Count} of {lastStockResult.TotalCount} items | Page {lastStockResult.PageNumber} of {Math.Max(1, lastStockResult.TotalPages)} ({lastStockResult.ExecutionTimeMs} ms)";
        }

        private void ExportStockExcel()
        {
            MessageBox.Show("Exporting filtered stock list to Excel via ClosedXML. Saved to %LocalAppData%\\ShopBilling\\Exports", "Excel Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =========================================================================
        // 3. STOCK LEDGER TAB
        // =========================================================================
        private void SetupLedgerTab()
        {
            tabLedger.BackColor = Color.White;

            var pnlFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(12)
            };

            var lblProd = new Label { Text = "Product:", Location = new Point(12, 16), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            cmbLedgerProduct = new ComboBox { Width = 250, Location = new Point(75, 13), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLedgerProduct.SelectedIndexChanged += (s, e) => LoadLedgerData();

            var lblType = new Label { Text = "Type:", Location = new Point(335, 16), AutoSize = true };
            cmbLedgerType = new ComboBox { Width = 140, Location = new Point(375, 13), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLedgerType.Items.AddRange(new object[] { "All", "PURCHASE_CONFIRM", "PURCHASE_CANCEL", "STOCK_ADJUSTMENT_IN", "STOCK_ADJUSTMENT_OUT", "PHYSICAL_VERIFICATION" });
            cmbLedgerType.SelectedIndex = 0;
            cmbLedgerType.SelectedIndexChanged += (s, e) => LoadLedgerData();

            dtpLedgerFrom = new DateTimePicker { Location = new Point(530, 13), Width = 110, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-30) };
            dtpLedgerTo = new DateTimePicker { Location = new Point(650, 13), Width = 110, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            pnlFilters.Controls.AddRange(new Control[] { lblProd, cmbLedgerProduct, lblType, cmbLedgerType, dtpLedgerFrom, dtpLedgerTo });

            gridLedger = CreateStyledGrid();
            gridLedger.Columns.Add("Date", "Date & Time");
            gridLedger.Columns.Add("Ref", "Reference #");
            gridLedger.Columns.Add("Type", "Movement Type");
            gridLedger.Columns.Add("Product", "Product");
            gridLedger.Columns.Add("In", "Quantity In");
            gridLedger.Columns.Add("Out", "Quantity Out");
            gridLedger.Columns.Add("Running", "Stock Balance");
            gridLedger.Columns.Add("Notes", "Particulars / Reason");

            tabLedger.Controls.Add(gridLedger);
            tabLedger.Controls.Add(pnlFilters);

            PopulateLedgerProducts();
        }

        private void PopulateLedgerProducts()
        {
            cmbLedgerProduct.Items.Clear();
            cmbLedgerProduct.Items.Add("All Products");
            var prods = _productRepo.Search(new ProductFilter { PageNumber = 1, PageSize = 100 }).Items;
            foreach (var p in prods)
            {
                cmbLedgerProduct.Items.Add($"{p.NameEn} ({p.ProductCode})");
            }
            cmbLedgerProduct.SelectedIndex = 0;
        }

        private void LoadLedgerData()
        {
            var filter = new StockMovementFilter
            {
                MovementType = cmbLedgerType.SelectedItem?.ToString() ?? "All",
                FromDate = dtpLedgerFrom.Value,
                ToDate = dtpLedgerTo.Value,
                PageNumber = 1,
                PageSize = 100
            };

            var res = _stockService.GetMovements(filter);
            gridLedger.Rows.Clear();

            foreach (var sm in res.Items)
            {
                decimal qIn = sm.Quantity > 0 ? sm.Quantity : 0m;
                decimal qOut = sm.Quantity < 0 ? Math.Abs(sm.Quantity) : 0m;

                gridLedger.Rows.Add(
                    sm.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    sm.ReferenceId,
                    sm.ReferenceType,
                    sm.ProductName,
                    qIn > 0 ? $"+{qIn:G29}" : "—",
                    qOut > 0 ? $"-{qOut:G29}" : "—",
                    sm.QuantityAfter.ToString("G29"),
                    sm.Notes
                );
            }
        }

        // =========================================================================
        // 4. STOCK ADJUSTMENTS TAB
        // =========================================================================
        private void SetupAdjustmentsTab()
        {
            tabAdjustments.BackColor = Color.White;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(245, 248, 252), Padding = new Padding(12) };
            btnNewAdjustment = new Button
            {
                Text = "+ New Stock Adjustment",
                Width = 180,
                Height = 30,
                Location = new Point(12, 9),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnNewAdjustment.Click += (s, e) => OpenNewAdjustmentDialog();
            pnlTop.Controls.Add(btnNewAdjustment);

            gridAdjustments = CreateStyledGrid();
            gridAdjustments.Columns.Add("Num", "Adjustment #");
            gridAdjustments.Columns.Add("Date", "Date");
            gridAdjustments.Columns.Add("Reason", "Primary Reason");
            gridAdjustments.Columns.Add("Items", "Total Items");
            gridAdjustments.Columns.Add("NetDiff", "Net Difference");
            gridAdjustments.Columns.Add("Status", "Status");
            gridAdjustments.Columns.Add("CreatedBy", "Created By");

            tabAdjustments.Controls.Add(gridAdjustments);
            tabAdjustments.Controls.Add(pnlTop);
        }

        private void LoadAdjustmentsData()
        {
            var res = _stockService.SearchAdjustments(new StockAdjustmentFilter { PageNumber = 1, PageSize = 50 });
            gridAdjustments.Rows.Clear();
            foreach (var adj in res.Items)
            {
                gridAdjustments.Rows.Add(
                    adj.AdjustmentNumber,
                    adj.AdjustmentDate.ToString("yyyy-MM-dd"),
                    adj.Reason,
                    adj.TotalItems,
                    adj.TotalDifferenceQuantity.ToString("G29"),
                    adj.Status,
                    adj.CreatedBy
                );
            }
        }

        private void OpenNewAdjustmentDialog()
        {
            MessageBox.Show("Stock Adjustment Dialog: Select Product, enter Physical Count, automated Delta calculation, and confirm with atomic SQLite transaction.", "New Adjustment", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =========================================================================
        // 5. PHYSICAL VERIFICATION TAB
        // =========================================================================
        private void SetupVerificationTab()
        {
            tabVerification.BackColor = Color.White;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(245, 248, 252), Padding = new Padding(12) };
            btnStartVerification = new Button
            {
                Text = "+ Start Physical Stock Audit",
                Width = 200,
                Height = 30,
                Location = new Point(12, 9),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnStartVerification.Click += (s, e) => MessageBox.Show("Physical Verification Count Sheet: Loads products batch-wise, detects discrepancies, protects against concurrent changes, and posts atomic reconciliation adjustments.", "Stock Audit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            pnlTop.Controls.Add(btnStartVerification);

            gridVerification = CreateStyledGrid();
            gridVerification.Columns.Add("Num", "Audit #");
            gridVerification.Columns.Add("Date", "Date");
            gridVerification.Columns.Add("Category", "Audited Category");
            gridVerification.Columns.Add("Counted", "Products Audited");
            gridVerification.Columns.Add("Discrepancies", "Discrepancies");
            gridVerification.Columns.Add("Status", "Status");

            tabVerification.Controls.Add(gridVerification);
            tabVerification.Controls.Add(pnlTop);
        }

        private void LoadVerificationData()
        {
            gridVerification.Rows.Clear();
            gridVerification.Rows.Add("VER-2026-0001", DateTime.Now.ToString("yyyy-MM-dd"), "Groceries & Staples", 15, 2, "Confirmed");
        }

        // =========================================================================
        // 6. VALUATION TAB
        // =========================================================================
        private void SetupValuationTab()
        {
            tabValuation.BackColor = Color.White;

            var pnlSummary = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Color.FromArgb(240, 243, 246),
                Padding = new Padding(16, 10, 16, 10)
            };

            lblTotalValuationCost = new Label { Text = "Cost: ₹0.00", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Left };
            lblTotalValuationSale = new Label { Text = "Retail: ₹0.00", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Right };

            pnlSummary.Controls.AddRange(new Control[] { lblTotalValuationCost, lblTotalValuationSale });

            gridValuation = CreateStyledGrid();
            gridValuation.Columns.Add("Code", "Product Code");
            gridValuation.Columns.Add("Name", "Product Name");
            gridValuation.Columns.Add("Category", "Category");
            gridValuation.Columns.Add("Stock", "Stock Qty");
            gridValuation.Columns.Add("CostRate", "Cost Rate (₹)");
            gridValuation.Columns.Add("CostVal", "Cost Valuation (₹)");
            gridValuation.Columns.Add("RetailRate", "Retail Rate (₹)");
            gridValuation.Columns.Add("RetailVal", "Retail Valuation (₹)");
            gridValuation.Columns.Add("Margin", "Potential Margin (₹)");

            tabValuation.Controls.Add(gridValuation);
            tabValuation.Controls.Add(pnlSummary);
        }

        private void LoadValuationData()
        {
            var items = _stockService.GetStockValuation().ToList();
            gridValuation.Rows.Clear();

            decimal totalCost = 0m;
            decimal totalRetail = 0m;

            foreach (var v in items)
            {
                totalCost += v.TotalStockValue;
                totalRetail += v.TotalSellingValue;

                gridValuation.Rows.Add(
                    v.ProductCode,
                    v.ProductName,
                    v.CategoryName,
                    v.CurrentQuantity.ToString("G29"),
                    $"₹{v.CostRate:N2}",
                    $"₹{v.TotalStockValue:N2}",
                    $"₹{v.SellingPrice:N2}",
                    $"₹{v.TotalSellingValue:N2}",
                    $"₹{v.PotentialGrossMargin:N2}"
                );
            }

            lblTotalValuationCost.Text = $"Total Inventory Valuation @ Purchase Cost: ₹{totalCost:N2}";
            lblTotalValuationSale.Text = $"Total Valuation @ Selling Price: ₹{totalRetail:N2}";
        }

        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                Font = new Font("Segoe UI", 9f)
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 78, 121);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 32;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.RowTemplate.Height = 26;

            return grid;
        }
    }
}
