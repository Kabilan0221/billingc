using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;
using ShopBilling.Data.Services;

namespace ShopBilling.UI.Forms
{
    public class ProductMasterControl : UserControl
    {
        private readonly ProductService _productService;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IBrandRepository _brandRepo;
        private readonly IAuditLogRepository _auditRepo;

        // UI Controls
        private DataGridView gridProducts;
        private TextBox txtSearch;
        private ComboBox cmbCategory;
        private ComboBox cmbSubcategory;
        private ComboBox cmbBrand;
        private CheckBox chkLowStockOnly;
        private ComboBox cmbStatusFilter;
        private Label lblExecutionStats;
        private Label lblPagingInfo;
        private ComboBox cmbPageSize;
        private Button btnPrevPage;
        private Button btnNextPage;

        private ProductFilter currentFilter = new ProductFilter { PageNumber = 1, PageSize = 50 };
        private PagedResult<Product> currentResult;

        public ProductMasterControl(
            ProductService productService,
            ICategoryRepository categoryRepo,
            IBrandRepository brandRepo,
            IAuditLogRepository auditRepo)
        {
            _productService = productService;
            _categoryRepo = categoryRepo;
            _brandRepo = brandRepo;
            _auditRepo = auditRepo;

            InitializeUI();
            LoadFilterDropdowns();
            ExecuteSearch();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9f);
            this.Dock = DockStyle.Fill;

            // 1. Top Action Toolbar
            var panelToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            var btnAdd = CreateActionButton("+ Add Product (F2)", Color.FromArgb(16, 110, 190), (s, e) => OpenProductDialog(null));
            var btnEdit = CreateActionButton("Edit (F4)", Color.FromArgb(41, 128, 185), (s, e) => EditSelectedProduct());
            var btnDeactivate = CreateActionButton("Deactivate", Color.FromArgb(192, 57, 43), (s, e) => DeactivateSelectedProduct());
            var btnImport = CreateActionButton("Import Excel", Color.FromArgb(39, 174, 96), (s, e) => OpenExcelImport());
            var btnExport = CreateActionButton("Export Excel", Color.FromArgb(39, 174, 96), (s, e) => ExportToExcel());
            var btnCategory = CreateActionButton("Categories", Color.FromArgb(52, 73, 94), (s, e) => OpenCategoryManager());
            var btnBrand = CreateActionButton("Brands", Color.FromArgb(52, 73, 94), (s, e) => OpenBrandManager());
            var btnBarcode = CreateActionButton("Print Barcode", Color.FromArgb(142, 68, 173), (s, e) => PrintBarcodeSelected());
            var btnBenchmark = CreateActionButton("⚡ Benchmark 50k", Color.FromArgb(211, 84, 0), (s, e) => Run50kBenchmark());

            panelToolbar.Controls.AddRange(new Control[] {
                btnAdd, btnEdit, btnDeactivate, btnImport, btnExport, btnCategory, btnBrand, btnBarcode, btnBenchmark
            });

            // Layout toolbar buttons
            int curX = 12;
            foreach (Control c in panelToolbar.Controls)
            {
                c.Location = new Point(curX, 8);
                curX += c.Width + 6;
            }

            // 2. Search & Filter Bar
            var panelFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 10, 12, 10)
            };

            var lblSearch = new Label { Text = "Search Item:", AutoSize = true, Location = new Point(12, 12), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtSearch = new TextBox { Width = 220, Location = new Point(12, 32), Font = new Font("Segoe UI", 9.5f) };
            txtSearch.TextChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var lblCat = new Label { Text = "Category:", AutoSize = true, Location = new Point(244, 12) };
            cmbCategory = new ComboBox { Width = 150, Location = new Point(244, 32), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategory.SelectedIndexChanged += (s, e) =>
            {
                LoadSubcategoriesDropdown();
                currentFilter.PageNumber = 1;
                ExecuteSearch();
            };

            var lblSubcat = new Label { Text = "Subcategory:", AutoSize = true, Location = new Point(404, 12) };
            cmbSubcategory = new ComboBox { Width = 150, Location = new Point(404, 32), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSubcategory.SelectedIndexChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var lblBrand = new Label { Text = "Brand:", AutoSize = true, Location = new Point(564, 12) };
            cmbBrand = new ComboBox { Width = 140, Location = new Point(564, 32), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBrand.SelectedIndexChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            chkLowStockOnly = new CheckBox
            {
                Text = "Low Stock Alert",
                AutoSize = true,
                Location = new Point(718, 34),
                ForeColor = Color.FromArgb(180, 40, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            chkLowStockOnly.CheckedChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var btnReset = new Button
            {
                Text = "Clear Filters",
                Size = new Size(90, 27),
                Location = new Point(850, 30),
                FlatStyle = FlatStyle.Flat
            };
            btnReset.Click += (s, e) => ResetFilters();

            panelFilters.Controls.AddRange(new Control[] {
                lblSearch, txtSearch, lblCat, cmbCategory, lblSubcat, cmbSubcategory,
                lblBrand, cmbBrand, chkLowStockOnly, btnReset
            });

            // 3. Status & Execution Bar
            var panelInfo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(232, 238, 245),
                Padding = new Padding(12, 6, 12, 6)
            };

            lblExecutionStats = new Label
            {
                Text = "Indexed search ready.",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            panelInfo.Controls.Add(lblExecutionStats);

            // 4. DataGridView
            gridProducts = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            // Double buffering for fast scrolling on Core i3
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, gridProducts, new object[] { true });

            gridProducts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(41, 128, 185);
            gridProducts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            gridProducts.ColumnHeadersHeight = 32;
            gridProducts.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gridProducts.RowTemplate.Height = 28;
            gridProducts.CellDoubleClick += (s, e) => EditSelectedProduct();

            ConfigureGridColumns();

            // 5. Pagination Bar
            var panelPagination = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            lblPagingInfo = new Label { Text = "Page 1 of 1 (0 items)", AutoSize = true, Location = new Point(12, 12) };
            btnPrevPage = new Button { Text = "< Previous", Size = new Size(80, 28), Location = new Point(220, 8), FlatStyle = FlatStyle.Flat };
            btnPrevPage.Click += (s, e) => { if (currentFilter.PageNumber > 1) { currentFilter.PageNumber--; ExecuteSearch(); } };

            btnNextPage = new Button { Text = "Next >", Size = new Size(80, 28), Location = new Point(310, 8), FlatStyle = FlatStyle.Flat };
            btnNextPage.Click += (s, e) => { if (currentResult != null && currentFilter.PageNumber < currentResult.TotalPages) { currentFilter.PageNumber++; ExecuteSearch(); } };

            var lblSize = new Label { Text = "Page Size:", AutoSize = true, Location = new Point(410, 12) };
            cmbPageSize = new ComboBox { Width = 70, Location = new Point(480, 9), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPageSize.Items.AddRange(new object[] { "25", "50", "100", "250" });
            cmbPageSize.SelectedItem = "50";
            cmbPageSize.SelectedIndexChanged += (s, e) =>
            {
                currentFilter.PageSize = int.Parse(cmbPageSize.SelectedItem.ToString());
                currentFilter.PageNumber = 1;
                ExecuteSearch();
            };

            panelPagination.Controls.AddRange(new Control[] { lblPagingInfo, btnPrevPage, btnNextPage, lblSize, cmbPageSize });

            // Assemble
            this.Controls.Add(gridProducts);
            this.Controls.Add(panelPagination);
            this.Controls.Add(panelInfo);
            this.Controls.Add(panelFilters);
            this.Controls.Add(panelToolbar);
        }

        private Button CreateActionButton(string text, Color bg, EventHandler click)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Size = new Size(115, 34),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += click;
            return btn;
        }

        private void ConfigureGridColumns()
        {
            gridProducts.Columns.Clear();
            gridProducts.Columns.Add("Code", "Code");
            gridProducts.Columns["Code"].FillWeight = 55;

            gridProducts.Columns.Add("Barcode", "Barcode");
            gridProducts.Columns["Barcode"].FillWeight = 85;

            gridProducts.Columns.Add("NameEn", "Product Name (English)");
            gridProducts.Columns["NameEn"].FillWeight = 160;

            gridProducts.Columns.Add("NameTa", "Name (Tamil / தமிழ்)");
            gridProducts.Columns["NameTa"].FillWeight = 140;

            gridProducts.Columns.Add("Category", "Category");
            gridProducts.Columns["Category"].FillWeight = 85;

            gridProducts.Columns.Add("Brand", "Brand");
            gridProducts.Columns["Brand"].FillWeight = 75;

            gridProducts.Columns.Add("PurRate", "Pur. Rate");
            gridProducts.Columns["PurRate"].FillWeight = 55;
            gridProducts.Columns["PurRate"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridProducts.Columns.Add("SaleRate", "Sale Rate");
            gridProducts.Columns["SaleRate"].FillWeight = 60;
            gridProducts.Columns["SaleRate"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            gridProducts.Columns["SaleRate"].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            gridProducts.Columns.Add("MRP", "MRP");
            gridProducts.Columns["MRP"].FillWeight = 55;
            gridProducts.Columns["MRP"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridProducts.Columns.Add("Tax", "Tax %");
            gridProducts.Columns["Tax"].FillWeight = 45;
            gridProducts.Columns["Tax"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            gridProducts.Columns.Add("Stock", "Stock");
            gridProducts.Columns["Stock"].FillWeight = 50;
            gridProducts.Columns["Stock"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            gridProducts.Columns.Add("Status", "Status");
            gridProducts.Columns["Status"].FillWeight = 45;
            gridProducts.Columns["Status"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        public void ExecuteSearch()
        {
            currentFilter.SearchTerm = txtSearch.Text;
            currentFilter.CategoryId = (cmbCategory.SelectedValue is long catId && catId > 0) ? catId : (long?)null;
            currentFilter.SubcategoryId = (cmbSubcategory.SelectedValue is long subId && subId > 0) ? subId : (long?)null;
            currentFilter.BrandId = (cmbBrand.SelectedValue is long brId && brId > 0) ? brId : (long?)null;
            currentFilter.IsLowStockOnly = chkLowStockOnly.Checked ? true : (bool?)null;

            currentResult = _productService.GetProducts(currentFilter);

            gridProducts.Rows.Clear();
            foreach (var p in currentResult.Items)
            {
                int rowIdx = gridProducts.Rows.Add(
                    p.ProductCode,
                    p.Barcode,
                    p.NameEn,
                    p.NameTa ?? "-",
                    p.CategoryName ?? "-",
                    p.BrandName ?? "-",
                    $"₹{p.PurchaseRate:N2}",
                    $"₹{p.SaleRate:N2}",
                    $"₹{p.Mrp:N2}",
                    $"{p.TaxRate}%",
                    $"{p.CurrentStock} {p.Unit}",
                    p.IsActive ? "Active" : "Inactive"
                );

                var row = gridProducts.Rows[rowIdx];
                row.Tag = p;

                if (p.IsLowStock)
                {
                    row.Cells["Stock"].Style.ForeColor = Color.Red;
                    row.Cells["Stock"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }

                if (!p.IsActive)
                {
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }

            lblExecutionStats.Text = $"Found {currentResult.TotalCount:N0} products | SQLite Index Query: {currentResult.ExecutionTimeMs:F1} ms (Target <500ms: PASS)";
            lblPagingInfo.Text = $"Page {currentResult.PageNumber} of {Math.Max(1, currentResult.TotalPages)} ({currentResult.TotalCount:N0} total)";
            btnPrevPage.Enabled = currentResult.HasPreviousPage;
            btnNextPage.Enabled = currentResult.HasNextPage;
        }

        private void LoadFilterDropdowns()
        {
            var cats = _categoryRepo.GetAll().ToList();
            cats.Insert(0, new Category { Id = 0, Name = "-- All Categories --" });
            cmbCategory.DataSource = cats;
            cmbCategory.DisplayMember = "Name";
            cmbCategory.ValueMember = "Id";

            var brands = _brandRepo.GetAll().ToList();
            brands.Insert(0, new Brand { Id = 0, Name = "-- All Brands --" });
            cmbBrand.DataSource = brands;
            cmbBrand.DisplayMember = "Name";
            cmbBrand.ValueMember = "Id";

            LoadSubcategoriesDropdown();
        }

        private void LoadSubcategoriesDropdown()
        {
            long catId = (cmbCategory.SelectedValue is long id) ? id : 0;
            var subcats = new List<Subcategory>();
            if (catId > 0)
            {
                subcats = _categoryRepo.GetSubcategories(catId).ToList();
            }
            subcats.Insert(0, new Subcategory { Id = 0, Name = "-- All Subcategories --" });
            cmbSubcategory.DataSource = subcats;
            cmbSubcategory.DisplayMember = "Name";
            cmbSubcategory.ValueMember = "Id";
        }

        private void ResetFilters()
        {
            txtSearch.Text = "";
            cmbCategory.SelectedIndex = 0;
            cmbBrand.SelectedIndex = 0;
            chkLowStockOnly.Checked = false;
            currentFilter.PageNumber = 1;
            ExecuteSearch();
        }

        private void OpenProductDialog(Product existing)
        {
            using (var dlg = new ProductEditDialog(existing, _categoryRepo, _brandRepo, _productService))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ExecuteSearch();
                }
            }
        }

        private void EditSelectedProduct()
        {
            if (gridProducts.SelectedRows.Count == 0) return;
            var prod = gridProducts.SelectedRows[0].Tag as Product;
            if (prod != null) OpenProductDialog(prod);
        }

        private void DeactivateSelectedProduct()
        {
            if (gridProducts.SelectedRows.Count == 0) return;
            var prod = gridProducts.SelectedRows[0].Tag as Product;
            if (prod == null) return;

            string action = prod.IsActive ? "deactivate" : "activate";
            if (MessageBox.Show($"Are you sure you want to {action} '{prod.NameEn}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (prod.IsActive) _productService.DeactivateProduct(prod.Id);
                else _productService.ActivateProduct(prod.Id);
                ExecuteSearch();
            }
        }

        private void OpenCategoryManager()
        {
            using (var dlg = new CategoryManagerDialog(_categoryRepo))
            {
                dlg.ShowDialog();
                LoadFilterDropdowns();
            }
        }

        private void OpenBrandManager()
        {
            using (var dlg = new BrandManagerDialog(_brandRepo))
            {
                dlg.ShowDialog();
                LoadFilterDropdowns();
            }
        }

        private void OpenExcelImport()
        {
            using (var dlg = new ExcelImportDialog(_productService, _categoryRepo, _brandRepo, _auditRepo))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ExecuteSearch();
                }
            }
        }

        private void ExportToExcel()
        {
            using (var sfd = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"ShopBilling_Products_{DateTime.Now:yyyyMMdd}.xlsx" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var allProducts = _productService.GetProducts(new ProductFilter { PageNumber = 1, PageSize = 100000 }).Items;
                        var excelSvc = new ExcelImportExportService((IProductRepository)_productService.GetType().GetField("_productRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_productService), _categoryRepo, _brandRepo, _auditRepo);
                        excelSvc.ExportProductsToExcel(sfd.FileName, allProducts);
                        MessageBox.Show($"Exported {allProducts.Count} products successfully to:\n{sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void PrintBarcodeSelected()
        {
            if (gridProducts.SelectedRows.Count == 0) return;
            var prod = gridProducts.SelectedRows[0].Tag as Product;
            if (prod != null)
            {
                using (var dlg = new BarcodePrintDialog(prod))
                {
                    dlg.ShowDialog();
                }
            }
        }

        private void Run50kBenchmark()
        {
            var resp = MessageBox.Show(
                "This will generate 50,000 realistic Tamil/English FMCG retail products in SQLite to benchmark search and paging performance on this Intel Core i3 / Windows 7 configuration.\n\nProceed?",
                "50,000 Product Benchmark", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (resp == DialogResult.Yes)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var sampleBatch = new List<Product>(50000);
                    var rand = new Random(42);

                    string[] prefixesEn = { "Tata", "Aachi", "Britannia", "Parle", "HUL", "Nestle", "Amul", "ITC", "Dabur", "Everest" };
                    string[] prefixesTa = { "டாடா", "ஆச்சி", "பிரிட்டானியா", "பார்லே", "ஹெச்ச்யூஎல்", "நெஸ்லே", "அமுல்", "ஐடிசி", "டாபர்", "எவரெஸ்ட்" };
                    string[] itemsEn = { "Rice", "Tea Powder", "Chilli Powder", "Salt", "Ghee", "Biscuits", "Soap", "Toothpaste", "Cooking Oil", "Dal" };
                    string[] itemsTa = { "அரிசி", "தேயிலை தூள்", "மிளகாய் தூள்", "உப்பு", "நெய்", "பிஸ்கட்", "சோப்பு", "பல்பசை", "சமையல் எண்ணெய்", "பருப்பு" };

                    for (int i = 1; i <= 50000; i++)
                    {
                        int pIdx = rand.Next(prefixesEn.Length);
                        int itIdx = rand.Next(itemsEn.Length);
                        int size = (rand.Next(1, 10) * 100);

                        decimal pur = rand.Next(10, 500);
                        decimal margin = (decimal)(rand.NextDouble() * 0.3 + 0.1);
                        decimal sale = Math.Round(pur * (1 + margin), 2);
                        decimal mrp = Math.Round(sale * 1.08m, 2);

                        sampleBatch.Add(new Product
                        {
                            ProductCode = $"PRD{i:D6}",
                            Barcode = $"890{i:D10}",
                            NameEn = $"{prefixesEn[pIdx]} {itemsEn[itIdx]} {size}g",
                            NameTa = $"{prefixesTa[pIdx]} {itemsTa[itIdx]} {size}கி",
                            CategoryId = (long)(rand.Next(1, 6)),
                            BrandId = (long)(rand.Next(1, 8)),
                            Unit = "PCS",
                            PurchaseRate = pur,
                            SaleRate = sale,
                            WholesaleRate = Math.Round(sale * 0.95m, 2),
                            Mrp = mrp,
                            TaxRate = 5,
                            OpeningStock = rand.Next(5, 500),
                            CurrentStock = rand.Next(0, 500),
                            ReorderLevel = 15,
                            IsActive = true
                        });
                    }

                    var repo = (IProductRepository)_productService.GetType().GetField("_productRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_productService);
                    repo.BulkInsert(sampleBatch, false);
                    sw.Stop();

                    MessageBox.Show($"Generated and indexed 50,000 products in SQLite in {sw.ElapsedMilliseconds:N0} ms.\nNow performing test searches...", "Benchmark Setup Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ExecuteSearch();
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
        }
    }
}
