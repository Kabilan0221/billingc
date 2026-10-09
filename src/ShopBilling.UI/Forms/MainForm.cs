using System;
using System.Drawing;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Services;
using ShopBilling.Data.Repositories;

namespace ShopBilling.UI.Forms
{
    public class MainForm : Form
    {
        private Panel panelSidebar;
        private Panel panelContent;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusDb;
        private ToolStripStatusLabel lblStatusItems;
        private ToolStripStatusLabel lblStatusClock;
        private Timer clockTimer;

        private Button[] navButtons;
        private int activeNavIndex = 2; // Default to Products (Index 2)

        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly ISupplierPaymentRepository _supplierPaymentRepository;
        private readonly IStockMovementRepository _stockMovementRepository;

        private readonly ProductService _productService;
        private readonly SupplierService _supplierService;
        private readonly PurchaseService _purchaseService;
        private readonly SupplierPaymentService _supplierPaymentService;

        private readonly StockRepository _stockRepository;
        private readonly StockService _stockService;

        public MainForm()
        {
            _productRepository = new ProductRepository();
            _categoryRepository = new CategoryRepository();
            _brandRepository = new BrandRepository();
            _auditLogRepository = new AuditLogRepository();
            _supplierRepository = new SupplierRepository();
            _purchaseRepository = new PurchaseRepository();
            _supplierPaymentRepository = new SupplierPaymentRepository();
            _stockMovementRepository = new StockMovementRepository();
            _stockRepository = new StockRepository();

            _productService = new ProductService(_productRepository, _auditLogRepository);
            _supplierService = new SupplierService(_supplierRepository, _auditLogRepository);
            _purchaseService = new PurchaseService(_purchaseRepository, _productRepository, _supplierRepository, _auditLogRepository);
            _supplierPaymentService = new SupplierPaymentService(_supplierPaymentRepository, _supplierRepository, _auditLogRepository);
            _stockService = new StockService(_stockRepository, _productRepository, _auditLogRepository);

            InitializeComponent();
            SetupShortcuts();
            LoadSection(2); // Load Products section
        }

        private void InitializeComponent()
        {
            this.Text = "ShopBilling - Commercial Offline Retail & Inventory Suite (Windows 7 SP1)";
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1024, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.KeyPreview = true;
            this.Icon = SystemIcons.Application;

            // Header/Brand bar
            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(24, 43, 73),
                Padding = new Padding(16, 0, 16, 0)
            };

            var lblLogo = new Label
            {
                Text = "SHOP BILLING PRO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(220, 56),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Left
            };

            var lblStoreInfo = new Label
            {
                Text = "Sri Murugan Super Market | Offline Mode (SQLite WAL) | Terminal #01",
                ForeColor = Color.FromArgb(180, 205, 237),
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = false,
                Size = new Size(500, 56),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Left
            };

            panelTop.Controls.Add(lblStoreInfo);
            panelTop.Controls.Add(lblLogo);

            // Sidebar
            panelSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                BackColor = Color.FromArgb(33, 43, 54),
                AutoScroll = true
            };

            // 14 Navigation Sections
            string[] navItems = new string[]
            {
                "Dashboard",
                "POS Billing — F1",
                "Products — F2",
                "Inbound Goods — F3",
                "Barcode Studio — F4",
                "Payments — F5",
                "Bill History — F6",
                "Suppliers — F7",
                "Stock — F8",
                "Estimates & Invoices — F9",
                "Reports — F10",
                "Backup & Restore",
                "Data Management",
                "Settings"
            };

            navButtons = new Button[navItems.Length];
            int yPos = 8;
            for (int i = 0; i < navItems.Length; i++)
            {
                int index = i;
                var btn = new Button
                {
                    Text = "  " + navItems[i],
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(200, 210, 225),
                    BackColor = (i == activeNavIndex) ? Color.FromArgb(16, 110, 190) : Color.FromArgb(33, 43, 54),
                    Font = new Font("Segoe UI", 9.5f, (i == activeNavIndex) ? FontStyle.Bold : FontStyle.Regular),
                    Size = new Size(210, 40),
                    Location = new Point(10, yPos),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 64, 80);
                btn.Click += (s, e) => LoadSection(index);

                panelSidebar.Controls.Add(btn);
                navButtons[i] = btn;
                yPos += 44;
            }

            // Main Content Area
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(243, 244, 246)
            };

            // Status Strip
            statusStrip = new StatusStrip();
            lblStatusDb = new ToolStripStatusLabel { Text = "Database: SQLite (WAL Active)", ForeColor = Color.DarkGreen };
            lblStatusItems = new ToolStripStatusLabel { Text = "Products: Loading...", BorderSides = ToolStripStatusLabelBorderSides.Left };
            lblStatusClock = new ToolStripStatusLabel { Text = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt"), BorderSides = ToolStripStatusLabelBorderSides.Left };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatusDb, lblStatusItems, lblStatusClock });

            clockTimer = new Timer { Interval = 1000 };
            clockTimer.Tick += (s, e) =>
            {
                lblStatusClock.Text = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt");
            };
            clockTimer.Start();

            this.Controls.Add(panelContent);
            this.Controls.Add(panelSidebar);
            this.Controls.Add(panelTop);
            this.Controls.Add(statusStrip);

            UpdateProductCountStatus();
        }

        private void SetupShortcuts()
        {
            this.KeyDown += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.F1: LoadSection(1); e.Handled = true; break; // POS Billing
                    case Keys.F2: LoadSection(2); e.Handled = true; break; // Products
                    case Keys.F3: LoadSection(3); e.Handled = true; break; // Inbound Goods
                    case Keys.F4: LoadSection(4); e.Handled = true; break; // Barcode Studio
                    case Keys.F5: LoadSection(5); e.Handled = true; break; // Payments
                    case Keys.F6: LoadSection(6); e.Handled = true; break; // Bill History
                    case Keys.F7: LoadSection(7); e.Handled = true; break; // Customers
                    case Keys.F8: LoadSection(8); e.Handled = true; break; // Stock
                    case Keys.F9: LoadSection(9); e.Handled = true; break; // Estimates
                    case Keys.F10: LoadSection(10); e.Handled = true; break; // Reports
                }
            };
        }

        public void LoadSection(int index)
        {
            activeNavIndex = index;
            for (int i = 0; i < navButtons.Length; i++)
            {
                bool isActive = (i == index);
                navButtons[i].BackColor = isActive ? Color.FromArgb(16, 110, 190) : Color.FromArgb(33, 43, 54);
                navButtons[i].ForeColor = isActive ? Color.White : Color.FromArgb(200, 210, 225);
                navButtons[i].Font = new Font("Segoe UI", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular);
            }

            panelContent.Controls.Clear();

            if (index == 2)
            {
                // Products Master (Phase 1 Core)
                var productControl = new ProductMasterControl(_productService, _categoryRepository, _brandRepository, _auditLogRepository);
                productControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(productControl);
            }
            else if (index == 3)
            {
                // Inbound Goods / Purchase Entry (Phase 2 Core)
                var inboundControl = new InboundGoodsControl(_purchaseService, _supplierService, _productService, _auditLogRepository);
                inboundControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(inboundControl);
            }
            else if (index == 6)
            {
                // Purchase History
                var historyControl = new PurchaseHistoryControl(_purchaseService, _supplierService);
                historyControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(historyControl);
            }
            else if (index == 7)
            {
                // Supplier Management (Module 1)
                var supplierControl = new SupplierManagementControl(_supplierService, _auditLogRepository);
                supplierControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(supplierControl);
            }
            else if (index == 5)
            {
                // Payments / Supplier Payment Entry
                var supplierControl = new SupplierManagementControl(_supplierService, _auditLogRepository);
                supplierControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(supplierControl);
                using (var dlg = new SupplierPaymentDialog(_supplierPaymentService, _supplierService))
                {
                    dlg.ShowDialog(this);
                }
            }
            else if (index == 8)
            {
                // Stock Management & Inventory (Phase 3 Core)
                var stockControl = new StockManagementControl(_stockService, _productRepository, _categoryRepository, _brandRepository, _auditLogRepository);
                stockControl.Dock = DockStyle.Fill;
                panelContent.Controls.Add(stockControl);
            }
            else
            {
                // Placeholder for future assigned phases with clean status card
                var pnlPlaceholder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32) };
                var lblTitle = new Label
                {
                    Text = navButtons[index].Text.Trim(),
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(24, 43, 73),
                    Dock = DockStyle.Top,
                    Height = 40
                };
                var lblMsg = new Label
                {
                    Text = $"This module is scheduled for implementation in upcoming phases.\nPhase 1 implements the complete Product Master, SQLite architecture, and 50,000 product indexing.\nUse 'Products — F2' or press F2 to open the Product Master.",
                    Font = new Font("Segoe UI", 10.5f),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Dock = DockStyle.Top,
                    Height = 90
                };
                var btnGoProducts = new Button
                {
                    Text = "Open Product Master (F2)",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 110, 190),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(200, 42),
                    Location = new Point(32, 140)
                };
                btnGoProducts.Click += (s, e) => LoadSection(2);

                pnlPlaceholder.Controls.Add(btnGoProducts);
                pnlPlaceholder.Controls.Add(lblMsg);
                pnlPlaceholder.Controls.Add(lblTitle);
                panelContent.Controls.Add(pnlPlaceholder);
            }

            UpdateProductCountStatus();
        }

        public void UpdateProductCountStatus()
        {
            try
            {
                var (total, low) = _productService.GetStockMetrics();
                lblStatusItems.Text = $"Active Catalog: {total:N0} products | Low Stock: {low:N0}";
            }
            catch
            {
                lblStatusItems.Text = "Products: Ready";
            }
        }
    }
}
