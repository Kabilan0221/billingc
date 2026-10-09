using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.UI.Forms
{
    public class ProductEditDialog : Form
    {
        private readonly Product _product;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IBrandRepository _brandRepo;
        private readonly ProductService _productService;

        private TextBox txtCode;
        private TextBox txtBarcode;
        private TextBox txtNameEn;
        private TextBox txtNameTa;
        private ComboBox cmbCategory;
        private ComboBox cmbSubcategory;
        private ComboBox cmbBrand;
        private ComboBox cmbUnit;
        private TextBox txtHsn;
        private NumericUpDown numPurRate;
        private NumericUpDown numSaleRate;
        private NumericUpDown numWsRate;
        private NumericUpDown numMrp;
        private ComboBox cmbTaxRate;
        private NumericUpDown numOpeningStock;
        private NumericUpDown numCurrentStock;
        private NumericUpDown numReorderLevel;
        private CheckBox chkIsActive;
        private Label lblMargin;
        private Label lblValidation;

        public ProductEditDialog(
            Product product,
            ICategoryRepository categoryRepo,
            IBrandRepository brandRepo,
            ProductService productService)
        {
            _product = product ?? new Product();
            _categoryRepo = categoryRepo;
            _brandRepo = brandRepo;
            _productService = productService;

            InitializeUI();
            PopulateData();
        }

        private void InitializeUI()
        {
            this.Text = (_product.Id == 0) ? "Product Master — New Product Creation" : $"Product Master — Edit Product ({_product.ProductCode})";
            this.Size = new Size(760, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            var panelHeader = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(24, 43, 73), Padding = new Padding(16, 12, 16, 0) };
            var lblTitle = new Label { Text = (_product.Id == 0) ? "Add New Product" : "Edit Product Details", ForeColor = Color.White, Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };
            panelHeader.Controls.Add(lblTitle);

            // Group: Basic Information
            var grpBasic = new GroupBox { Text = "Product Information & Identification", Location = new Point(16, 55), Size = new Size(710, 185) };

            var l1 = new Label { Text = "Product Code *:", Location = new Point(16, 26), AutoSize = true };
            txtCode = new TextBox { Location = new Point(120, 24), Width = 150 };

            var l2 = new Label { Text = "Barcode *:", Location = new Point(290, 26), AutoSize = true };
            txtBarcode = new TextBox { Location = new Point(370, 24), Width = 180 };
            var btnGenBarcode = new Button { Text = "Auto EAN-13", Location = new Point(560, 23), Size = new Size(90, 25) };
            btnGenBarcode.Click += (s, e) => txtBarcode.Text = ValidationService.GenerateEan13WithChecksum("890" + DateTime.Now.ToString("yyMMddHHm"));

            var l3 = new Label { Text = "Name (English) *:", Location = new Point(16, 58), AutoSize = true };
            txtNameEn = new TextBox { Location = new Point(120, 56), Width = 530 };

            var l4 = new Label { Text = "Name (Tamil) தமிழ்:", Location = new Point(16, 90), AutoSize = true };
            txtNameTa = new TextBox { Location = new Point(120, 88), Width = 530, Font = new Font("Segoe UI", 9.5f) };

            var l5 = new Label { Text = "Category:", Location = new Point(16, 122), AutoSize = true };
            cmbCategory = new ComboBox { Location = new Point(120, 120), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategory.SelectedIndexChanged += (s, e) => LoadSubcategories();

            var l6 = new Label { Text = "Subcategory:", Location = new Point(350, 122), AutoSize = true };
            cmbSubcategory = new ComboBox { Location = new Point(440, 120), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };

            var l7 = new Label { Text = "Brand:", Location = new Point(16, 154), AutoSize = true };
            cmbBrand = new ComboBox { Location = new Point(120, 152), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };

            var l8 = new Label { Text = "Unit / HSN:", Location = new Point(350, 154), AutoSize = true };
            cmbUnit = new ComboBox { Location = new Point(440, 152), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbUnit.Items.AddRange(new object[] { "PCS", "KG", "G", "LTR", "ML", "PACK", "BOX" });
            cmbUnit.SelectedItem = "PCS";

            txtHsn = new TextBox { Location = new Point(540, 152), Width = 110 };

            grpBasic.Controls.AddRange(new Control[] {
                l1, txtCode, l2, txtBarcode, btnGenBarcode, l3, txtNameEn, l4, txtNameTa,
                l5, cmbCategory, l6, cmbSubcategory, l7, cmbBrand, l8, cmbUnit, txtHsn
            });

            // Group: Pricing & Tax
            var grpPrice = new GroupBox { Text = "Pricing, Rates & GST Tax Structure", Location = new Point(16, 248), Size = new Size(710, 130) };

            var lp1 = new Label { Text = "Purchase Rate (₹):", Location = new Point(16, 26), AutoSize = true };
            numPurRate = CreateNumericInput(new Point(130, 24));

            var lp2 = new Label { Text = "Sale Rate (₹) *:", Location = new Point(255, 26), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            numSaleRate = CreateNumericInput(new Point(350, 24));
            numSaleRate.ValueChanged += (s, e) => UpdateMarginDisplay();
            numPurRate.ValueChanged += (s, e) => UpdateMarginDisplay();

            var lp3 = new Label { Text = "Wholesale Rate (₹):", Location = new Point(480, 26), AutoSize = true };
            numWsRate = CreateNumericInput(new Point(590, 24));

            var lp4 = new Label { Text = "MRP (₹) *:", Location = new Point(16, 62), AutoSize = true };
            numMrp = CreateNumericInput(new Point(130, 60));

            var lp5 = new Label { Text = "GST Tax %:", Location = new Point(255, 62), AutoSize = true };
            cmbTaxRate = new ComboBox { Location = new Point(350, 60), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTaxRate.Items.AddRange(new object[] { "0", "5", "12", "18", "28" });
            cmbTaxRate.SelectedItem = "5";

            lblMargin = new Label { Text = "Margin: 0.0%", Location = new Point(480, 62), AutoSize = true, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

            grpPrice.Controls.AddRange(new Control[] { lp1, numPurRate, lp2, numSaleRate, lp3, numWsRate, lp4, numMrp, lp5, cmbTaxRate, lblMargin });

            // Group: Inventory & Stock
            var grpStock = new GroupBox { Text = "Stock Control & Reorder Thresholds", Location = new Point(16, 386), Size = new Size(710, 85) };

            var ls1 = new Label { Text = "Opening Stock:", Location = new Point(16, 26), AutoSize = true };
            numOpeningStock = CreateNumericInput(new Point(120, 24));

            var ls2 = new Label { Text = "Current Stock:", Location = new Point(250, 26), AutoSize = true };
            numCurrentStock = CreateNumericInput(new Point(340, 24));

            var ls3 = new Label { Text = "Reorder Level:", Location = new Point(470, 26), AutoSize = true };
            numReorderLevel = CreateNumericInput(new Point(570, 24));
            numReorderLevel.Value = 5;

            chkIsActive = new CheckBox { Text = "Active Product (Available for billing)", Location = new Point(16, 56), AutoSize = true, Checked = true };

            grpStock.Controls.AddRange(new Control[] { ls1, numOpeningStock, ls2, numCurrentStock, ls3, numReorderLevel, chkIsActive });

            // Validation Warning label
            lblValidation = new Label { Text = "", ForeColor = Color.Red, Location = new Point(16, 480), Size = new Size(710, 30), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            // Buttons panel
            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.White, Padding = new Padding(16, 10, 16, 10) };
            var btnSave = new Button { Text = "Save Product (F2)", BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(130, 36), Dock = DockStyle.Right, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSave.Click += (s, e) => SaveProduct();

            var btnCancel = new Button { Text = "Cancel (Esc)", FlatStyle = FlatStyle.Flat, Size = new Size(90, 36), Dock = DockStyle.Right };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            panelBottom.Controls.Add(btnCancel);
            panelBottom.Controls.Add(btnSave);

            this.Controls.Add(panelBottom);
            this.Controls.Add(lblValidation);
            this.Controls.Add(grpStock);
            this.Controls.Add(grpPrice);
            this.Controls.Add(grpBasic);
            this.Controls.Add(panelHeader);

            // Shortcuts
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) this.DialogResult = DialogResult.Cancel;
                if (e.KeyCode == Keys.F2 || (e.Control && e.KeyCode == Keys.S)) SaveProduct();
            };
        }

        private NumericUpDown CreateNumericInput(Point location)
        {
            return new NumericUpDown
            {
                Location = location,
                Width = 100,
                DecimalPlaces = 2,
                Maximum = 9999999,
                Minimum = 0
            };
        }

        private void PopulateData()
        {
            var cats = _categoryRepo.GetAll().ToList();
            cmbCategory.DataSource = cats;
            cmbCategory.DisplayMember = "Name";
            cmbCategory.ValueMember = "Id";

            var brands = _brandRepo.GetAll().ToList();
            cmbBrand.DataSource = brands;
            cmbBrand.DisplayMember = "Name";
            cmbBrand.ValueMember = "Id";

            if (_product.Id != 0)
            {
                txtCode.Text = _product.ProductCode;
                txtBarcode.Text = _product.Barcode;
                txtNameEn.Text = _product.NameEn;
                txtNameTa.Text = _product.NameTa;
                if (_product.CategoryId.HasValue) cmbCategory.SelectedValue = _product.CategoryId.Value;
                LoadSubcategories();
                if (_product.SubcategoryId.HasValue) cmbSubcategory.SelectedValue = _product.SubcategoryId.Value;
                if (_product.BrandId.HasValue) cmbBrand.SelectedValue = _product.BrandId.Value;
                cmbUnit.SelectedItem = _product.Unit;
                txtHsn.Text = _product.HsnCode;

                numPurRate.Value = _product.PurchaseRate;
                numSaleRate.Value = _product.SaleRate;
                numWsRate.Value = _product.WholesaleRate;
                numMrp.Value = _product.Mrp;
                cmbTaxRate.SelectedItem = _product.TaxRate.ToString("0");

                numOpeningStock.Value = _product.OpeningStock;
                numCurrentStock.Value = _product.CurrentStock;
                numReorderLevel.Value = _product.ReorderLevel;
                chkIsActive.Checked = _product.IsActive;
            }
            else
            {
                // Defaults for new product
                txtCode.Text = "PRD" + DateTime.Now.ToString("HHmmss");
                txtBarcode.Text = ValidationService.GenerateEan13WithChecksum("890" + DateTime.Now.ToString("yyMMddHHm"));
                LoadSubcategories();
            }

            UpdateMarginDisplay();
        }

        private void LoadSubcategories()
        {
            if (cmbCategory.SelectedValue is long catId && catId > 0)
            {
                var subcats = _categoryRepo.GetSubcategories(catId).ToList();
                cmbSubcategory.DataSource = subcats;
                cmbSubcategory.DisplayMember = "Name";
                cmbSubcategory.ValueMember = "Id";
            }
        }

        private void UpdateMarginDisplay()
        {
            decimal pur = numPurRate.Value;
            decimal sale = numSaleRate.Value;
            if (pur > 0)
            {
                decimal marginPct = ((sale - pur) / pur) * 100m;
                lblMargin.Text = $"Margin: {marginPct:F1}% (₹{sale - pur:N2})";
                lblMargin.ForeColor = (marginPct >= 0) ? Color.DarkGreen : Color.Red;
            }
            else
            {
                lblMargin.Text = "Margin: -";
            }
        }

        private void SaveProduct()
        {
            lblValidation.Text = "";

            _product.ProductCode = txtCode.Text?.Trim();
            _product.Barcode = txtBarcode.Text?.Trim();
            _product.NameEn = txtNameEn.Text?.Trim();
            _product.NameTa = string.IsNullOrWhiteSpace(txtNameTa.Text) ? null : txtNameTa.Text.Trim();
            _product.CategoryId = (cmbCategory.SelectedValue is long cId && cId > 0) ? cId : (long?)null;
            _product.SubcategoryId = (cmbSubcategory.SelectedValue is long sId && sId > 0) ? sId : (long?)null;
            _product.BrandId = (cmbBrand.SelectedValue is long bId && bId > 0) ? bId : (long?)null;
            _product.Unit = cmbUnit.SelectedItem?.ToString() ?? "PCS";
            _product.HsnCode = txtHsn.Text?.Trim();

            _product.PurchaseRate = numPurRate.Value;
            _product.SaleRate = numSaleRate.Value;
            _product.WholesaleRate = numWsRate.Value;
            _product.Mrp = numMrp.Value;
            _product.TaxRate = decimal.Parse(cmbTaxRate.SelectedItem.ToString());

            _product.OpeningStock = numOpeningStock.Value;
            _product.CurrentStock = numCurrentStock.Value;
            _product.ReorderLevel = numReorderLevel.Value;
            _product.IsActive = chkIsActive.Checked;

            var valResult = _productService.SaveProduct(_product);
            if (!valResult.IsValid)
            {
                lblValidation.Text = string.Join("\n", valResult.Errors);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
