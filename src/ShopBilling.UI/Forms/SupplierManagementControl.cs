using System;
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
    public class SupplierManagementControl : UserControl
    {
        private readonly SupplierService _supplierService;
        private readonly IAuditLogRepository _auditRepo;

        private DataGridView gridSuppliers;
        private TextBox txtSearch;
        private ComboBox cmbStatusFilter;
        private Label lblStats;
        private Label lblPaging;
        private Button btnPrev;
        private Button btnNext;

        private SupplierFilter currentFilter = new SupplierFilter { PageNumber = 1, PageSize = 50 };
        private PagedResult<Supplier> currentResult;

        public SupplierManagementControl(SupplierService supplierService, IAuditLogRepository auditRepo)
        {
            _supplierService = supplierService;
            _auditRepo = auditRepo;

            InitializeUI();
            ExecuteSearch();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9f);
            this.Dock = DockStyle.Fill;

            // 1. Toolbar
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            var btnAdd = CreateBtn("+ Add Supplier (F2)", Color.FromArgb(16, 110, 190), (s, e) => OpenEditDialog(null));
            var btnEdit = CreateBtn("Edit Supplier (F4)", Color.FromArgb(41, 128, 185), (s, e) => EditSelected());
            var btnDeactivate = CreateBtn("Deactivate", Color.FromArgb(192, 57, 43), (s, e) => DeactivateSelected());
            var btnExport = CreateBtn("Export Excel", Color.FromArgb(39, 174, 96), (s, e) => ExportExcel());
            var btnImport = CreateBtn("Import Excel", Color.FromArgb(39, 174, 96), (s, e) => ImportExcel());

            pnlToolbar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDeactivate, btnExport, btnImport });
            int curX = 12;
            foreach (Control c in pnlToolbar.Controls)
            {
                c.Location = new Point(curX, 8);
                curX += c.Width + 6;
            }

            // 2. Filter Bar
            var pnlFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 10, 12, 10)
            };

            var lblSearch = new Label { Text = "Search:", Location = new Point(12, 16), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtSearch = new TextBox { Width = 260, Location = new Point(65, 14), Font = new Font("Segoe UI", 9.5f) };
            txtSearch.TextChanged += (s, e) => { currentFilter.PageNumber = 1; ExecuteSearch(); };

            var lblStatus = new Label { Text = "Status:", Location = new Point(345, 16), AutoSize = true };
            cmbStatusFilter = new ComboBox { Width = 120, Location = new Point(395, 14), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatusFilter.Items.AddRange(new object[] { "Active Only", "Inactive Only", "All Suppliers" });
            cmbStatusFilter.SelectedIndex = 0;
            cmbStatusFilter.SelectedIndexChanged += (s, e) =>
            {
                currentFilter.IsActive = cmbStatusFilter.SelectedIndex == 0 ? true : cmbStatusFilter.SelectedIndex == 1 ? false : (bool?)null;
                currentFilter.PageNumber = 1;
                ExecuteSearch();
            };

            var btnReset = new Button { Text = "Reset", Location = new Point(530, 13), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat };
            btnReset.Click += (s, e) => { txtSearch.Text = ""; cmbStatusFilter.SelectedIndex = 0; };

            pnlFilters.Controls.AddRange(new Control[] { lblSearch, txtSearch, lblStatus, cmbStatusFilter, btnReset });

            // 3. Stats Strip
            var pnlStats = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(232, 238, 245), Padding = new Padding(12, 5, 12, 5) };
            lblStats = new Label { Text = "Ready.", Dock = DockStyle.Left, AutoSize = true, ForeColor = Color.FromArgb(30, 60, 90), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            pnlStats.Controls.Add(lblStats);

            // 4. DataGridView
            gridSuppliers = new DataGridView
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
                AutoSizeColumnsMode = DataGridViewColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            // Double buffering
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, gridSuppliers, new object[] { true });

            gridSuppliers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 78, 121);
            gridSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            gridSuppliers.ColumnHeadersHeight = 32;
            gridSuppliers.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gridSuppliers.RowTemplate.Height = 28;
            gridSuppliers.CellDoubleClick += (s, e) => EditSelected();

            ConfigureGridColumns();

            // 5. Paging
            var pnlPaging = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.White, Padding = new Padding(12, 6, 12, 6) };
            lblPaging = new Label { Text = "Page 1 of 1", AutoSize = true, Location = new Point(12, 10) };
            btnPrev = new Button { Text = "< Prev", Location = new Point(180, 6), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat };
            btnPrev.Click += (s, e) => { if (currentFilter.PageNumber > 1) { currentFilter.PageNumber--; ExecuteSearch(); } };
            btnNext = new Button { Text = "Next >", Location = new Point(260, 6), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat };
            btnNext.Click += (s, e) => { if (currentResult != null && currentFilter.PageNumber < currentResult.TotalPages) { currentFilter.PageNumber++; ExecuteSearch(); } };
            pnlPaging.Controls.AddRange(new Control[] { lblPaging, btnPrev, btnNext });

            this.Controls.Add(gridSuppliers);
            this.Controls.Add(pnlPaging);
            this.Controls.Add(pnlStats);
            this.Controls.Add(pnlFilters);
            this.Controls.Add(pnlToolbar);
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
            gridSuppliers.Columns.Clear();
            gridSuppliers.Columns.Add("Code", "Code");
            gridSuppliers.Columns["Code"].FillWeight = 50;

            gridSuppliers.Columns.Add("Name", "Supplier Name");
            gridSuppliers.Columns["Name"].FillWeight = 160;

            gridSuppliers.Columns.Add("Contact", "Contact Person");
            gridSuppliers.Columns["Contact"].FillWeight = 100;

            gridSuppliers.Columns.Add("Mobile", "Mobile Number");
            gridSuppliers.Columns["Mobile"].FillWeight = 85;

            gridSuppliers.Columns.Add("Gstin", "GSTIN");
            gridSuppliers.Columns["Gstin"].FillWeight = 95;

            gridSuppliers.Columns.Add("City", "City");
            gridSuppliers.Columns["City"].FillWeight = 70;

            gridSuppliers.Columns.Add("Balance", "Current Balance (₹)");
            gridSuppliers.Columns["Balance"].FillWeight = 85;
            gridSuppliers.Columns["Balance"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            gridSuppliers.Columns["Balance"].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            gridSuppliers.Columns.Add("Terms", "Payment Terms");
            gridSuppliers.Columns["Terms"].FillWeight = 75;

            gridSuppliers.Columns.Add("Status", "Status");
            gridSuppliers.Columns["Status"].FillWeight = 45;
            gridSuppliers.Columns["Status"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        public void ExecuteSearch()
        {
            currentFilter.SearchTerm = txtSearch.Text;
            currentResult = _supplierService.Search(currentFilter);

            gridSuppliers.Rows.Clear();
            foreach (var s in currentResult.Items)
            {
                int rIdx = gridSuppliers.Rows.Add(
                    s.Code,
                    s.Name,
                    s.ContactPerson ?? "-",
                    s.Mobile ?? "-",
                    s.Gstin ?? "-",
                    s.City ?? "-",
                    $"₹{s.CurrentBalance:N2}",
                    s.PaymentTerms ?? "-",
                    s.IsActive ? "Active" : "Inactive"
                );

                var row = gridSuppliers.Rows[rIdx];
                row.Tag = s;

                if (s.CurrentBalance > 0)
                {
                    row.Cells["Balance"].Style.ForeColor = Color.DarkRed;
                }
                if (!s.IsActive)
                {
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }

            lblStats.Text = $"Found {currentResult.TotalCount} suppliers | Query: {currentResult.ExecutionTimeMs:F1} ms";
            lblPaging.Text = $"Page {currentResult.PageNumber} of {Math.Max(1, currentResult.TotalPages)} ({currentResult.TotalCount} total)";
            btnPrev.Enabled = currentResult.HasPreviousPage;
            btnNext.Enabled = currentResult.HasNextPage;
        }

        private void OpenEditDialog(Supplier s)
        {
            using (var dlg = new SupplierEditDialog(s, _supplierService))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ExecuteSearch();
                }
            }
        }

        private void EditSelected()
        {
            if (gridSuppliers.SelectedRows.Count == 0) return;
            var s = gridSuppliers.SelectedRows[0].Tag as Supplier;
            if (s != null) OpenEditDialog(s);
        }

        private void DeactivateSelected()
        {
            if (gridSuppliers.SelectedRows.Count == 0) return;
            var s = gridSuppliers.SelectedRows[0].Tag as Supplier;
            if (s == null) return;

            string action = s.IsActive ? "deactivate" : "activate";
            if (MessageBox.Show($"Are you sure you want to {action} supplier '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (s.IsActive)
                {
                    string err;
                    if (!_supplierService.DeactivateSupplier(s.Id, out err))
                    {
                        MessageBox.Show(err, "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    _supplierService.ActivateSupplier(s.Id);
                }
                ExecuteSearch();
            }
        }

        private void ExportExcel()
        {
            using (var sfd = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"ShopBilling_Suppliers_{DateTime.Now:yyyyMMdd}.xlsx" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var repo = (ISupplierRepository)_supplierService.GetType().GetField("_supplierRepo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_supplierService);
                        var excelSvc = new SupplierExcelService(repo, _auditRepo);
                        var all = _supplierService.Search(new SupplierFilter { PageNumber = 1, PageSize = 10000 }).Items;
                        excelSvc.ExportSuppliersToExcel(sfd.FileName, all);
                        MessageBox.Show($"Exported {all.Count} suppliers successfully to:\n{sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportExcel()
        {
            using (var ofd = new OpenFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var repo = (ISupplierRepository)_supplierService.GetType().GetField("_supplierRepo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_supplierService);
                        var excelSvc = new SupplierExcelService(repo, _auditRepo);
                        var res = excelSvc.ImportSuppliersFromExcel(ofd.FileName);
                        MessageBox.Show($"Import Complete!\nSuccessfully Imported: {res.SuccessfullyImported}\nFailed Rows: {res.FailedCount}", "Import Summary", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ExecuteSearch();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

    public class SupplierEditDialog : Form
    {
        private readonly Supplier _supplier;
        private readonly SupplierService _supplierService;

        private TextBox txtCode;
        private TextBox txtName;
        private TextBox txtContact;
        private TextBox txtMobile;
        private TextBox txtAltMobile;
        private TextBox txtWhatsApp;
        private TextBox txtEmail;
        private TextBox txtGstin;
        private TextBox txtPan;
        private TextBox txtAddress;
        private TextBox txtCity;
        private TextBox txtState;
        private TextBox txtPin;
        private NumericUpDown numOpeningBalance;
        private ComboBox cmbTerms;
        private TextBox txtNotes;
        private CheckBox chkIsActive;
        private Label lblValidation;

        public SupplierEditDialog(Supplier supplier, SupplierService supplierService)
        {
            _supplier = supplier ?? new Supplier();
            _supplierService = supplierService;

            InitializeUI();
            PopulateData();
        }

        private void InitializeUI()
        {
            this.Text = _supplier.Id == 0 ? "Supplier Management — Add New Supplier" : $"Edit Supplier ({_supplier.Code})";
            this.Size = new Size(680, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(31, 78, 121), Padding = new Padding(14, 10, 14, 0) };
            var lblTitle = new Label { Text = _supplier.Id == 0 ? "Add New Supplier Record" : "Edit Supplier Profile", ForeColor = Color.White, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), AutoSize = true };
            pnlTop.Controls.Add(lblTitle);

            // Group: Identification
            var grpId = new GroupBox { Text = "Supplier Identification", Location = new Point(14, 52), Size = new Size(636, 120) };
            var l1 = new Label { Text = "Supplier Code *:", Location = new Point(14, 25), AutoSize = true };
            txtCode = new TextBox { Location = new Point(120, 23), Width = 150 };
            var btnGenCode = new Button { Text = "Auto Code", Location = new Point(280, 22), Size = new Size(80, 25) };
            btnGenCode.Click += (s, e) => txtCode.Text = "SUP" + DateTime.Now.ToString("HHmmss");

            var l2 = new Label { Text = "Supplier Name *:", Location = new Point(14, 55), AutoSize = true };
            txtName = new TextBox { Location = new Point(120, 53), Width = 480 };

            var l3 = new Label { Text = "Contact Person:", Location = new Point(14, 85), AutoSize = true };
            txtContact = new TextBox { Location = new Point(120, 83), Width = 480 };

            grpId.Controls.AddRange(new Control[] { l1, txtCode, btnGenCode, l2, txtName, l3, txtContact });

            // Group: Contact & Tax
            var grpContact = new GroupBox { Text = "Contact Numbers & Tax Registrations", Location = new Point(14, 180), Size = new Size(636, 115) };
            var l4 = new Label { Text = "Mobile *:", Location = new Point(14, 25), AutoSize = true };
            txtMobile = new TextBox { Location = new Point(80, 23), Width = 120 };

            var l5 = new Label { Text = "Alt Mobile:", Location = new Point(220, 25), AutoSize = true };
            txtAltMobile = new TextBox { Location = new Point(290, 23), Width = 120 };

            var l6 = new Label { Text = "WhatsApp:", Location = new Point(430, 25), AutoSize = true };
            txtWhatsApp = new TextBox { Location = new Point(500, 23), Width = 110 };

            var l7 = new Label { Text = "Email:", Location = new Point(14, 55), AutoSize = true };
            txtEmail = new TextBox { Location = new Point(80, 53), Width = 200 };

            var l8 = new Label { Text = "GSTIN:", Location = new Point(295, 55), AutoSize = true };
            txtGstin = new TextBox { Location = new Point(345, 53), Width = 150 };

            var l9 = new Label { Text = "PAN:", Location = new Point(505, 55), AutoSize = true };
            txtPan = new TextBox { Location = new Point(540, 53), Width = 70 };

            grpContact.Controls.AddRange(new Control[] { l4, txtMobile, l5, txtAltMobile, l6, txtWhatsApp, l7, txtEmail, l8, txtGstin, l9, txtPan });

            // Group: Address & Commercials
            var grpAddr = new GroupBox { Text = "Address, Commercials & Balances", Location = new Point(14, 305), Size = new Size(636, 140) };
            var l10 = new Label { Text = "Address:", Location = new Point(14, 22), AutoSize = true };
            txtAddress = new TextBox { Location = new Point(80, 20), Width = 310 };

            var l11 = new Label { Text = "City:", Location = new Point(400, 22), AutoSize = true };
            txtCity = new TextBox { Location = new Point(440, 20), Width = 170 };

            var l12 = new Label { Text = "State:", Location = new Point(14, 52), AutoSize = true };
            txtState = new TextBox { Location = new Point(80, 50), Width = 130, Text = "Tamil Nadu" };

            var l13 = new Label { Text = "PIN:", Location = new Point(220, 52), AutoSize = true };
            txtPin = new TextBox { Location = new Point(260, 50), Width = 80 };

            var l14 = new Label { Text = "Opening Balance ₹:", Location = new Point(350, 52), AutoSize = true };
            numOpeningBalance = new NumericUpDown { Location = new Point(470, 50), Width = 140, DecimalPlaces = 2, Maximum = 9999999 };

            var l15 = new Label { Text = "Terms:", Location = new Point(14, 82), AutoSize = true };
            cmbTerms = new ComboBox { Location = new Point(80, 80), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTerms.Items.AddRange(new object[] { "Immediate Cash", "7 Days Net", "15 Days Net", "30 Days Net", "45 Days Net" });
            cmbTerms.SelectedIndex = 3;

            var l16 = new Label { Text = "Notes:", Location = new Point(245, 82), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(290, 80), Width = 320 };

            chkIsActive = new CheckBox { Text = "Active Supplier", Location = new Point(80, 110), AutoSize = true, Checked = true };

            grpAddr.Controls.AddRange(new Control[] { l10, txtAddress, l11, txtCity, l12, txtState, l13, txtPin, l14, numOpeningBalance, l15, cmbTerms, l16, txtNotes, chkIsActive });

            // Validation label
            lblValidation = new Label { Text = "", ForeColor = Color.Red, Location = new Point(14, 452), Size = new Size(636, 25), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            // Bottom Buttons
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8) };
            var btnSave = new Button { Text = "Save Supplier", BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(130, 32), Dock = DockStyle.Right, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSave.Click += (s, e) => SaveSupplier();

            var btnCancel = new Button { Text = "Cancel", FlatStyle = FlatStyle.Flat, Size = new Size(80, 32), Dock = DockStyle.Right };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            pnlBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(btnSave);

            this.Controls.Add(pnlBottom);
            this.Controls.Add(lblValidation);
            this.Controls.Add(grpAddr);
            this.Controls.Add(grpContact);
            this.Controls.Add(grpId);
            this.Controls.Add(pnlTop);
        }

        private void PopulateData()
        {
            if (_supplier.Id != 0)
            {
                txtCode.Text = _supplier.Code;
                txtName.Text = _supplier.Name;
                txtContact.Text = _supplier.ContactPerson;
                txtMobile.Text = _supplier.Mobile;
                txtAltMobile.Text = _supplier.AltMobile;
                txtWhatsApp.Text = _supplier.WhatsApp;
                txtEmail.Text = _supplier.Email;
                txtGstin.Text = _supplier.Gstin;
                txtPan.Text = _supplier.Pan;
                txtAddress.Text = _supplier.Address;
                txtCity.Text = _supplier.City;
                txtState.Text = _supplier.State;
                txtPin.Text = _supplier.PinCode;
                numOpeningBalance.Value = _supplier.OpeningBalance;
                if (!string.IsNullOrEmpty(_supplier.PaymentTerms)) cmbTerms.SelectedItem = _supplier.PaymentTerms;
                txtNotes.Text = _supplier.Notes;
                chkIsActive.Checked = _supplier.IsActive;
            }
            else
            {
                txtCode.Text = "SUP" + DateTime.Now.ToString("HHmmss");
            }
        }

        private void SaveSupplier()
        {
            lblValidation.Text = "";

            _supplier.Code = txtCode.Text?.Trim();
            _supplier.Name = txtName.Text?.Trim();
            _supplier.ContactPerson = txtContact.Text?.Trim();
            _supplier.Mobile = txtMobile.Text?.Trim();
            _supplier.AltMobile = txtAltMobile.Text?.Trim();
            _supplier.WhatsApp = txtWhatsApp.Text?.Trim();
            _supplier.Email = txtEmail.Text?.Trim();
            _supplier.Gstin = txtGstin.Text?.Trim().ToUpper();
            _supplier.Pan = txtPan.Text?.Trim().ToUpper();
            _supplier.Address = txtAddress.Text?.Trim();
            _supplier.City = txtCity.Text?.Trim();
            _supplier.State = txtState.Text?.Trim() ?? "Tamil Nadu";
            _supplier.PinCode = txtPin.Text?.Trim();
            _supplier.OpeningBalance = numOpeningBalance.Value;
            _supplier.PaymentTerms = cmbTerms.SelectedItem?.ToString();
            _supplier.Notes = txtNotes.Text?.Trim();
            _supplier.IsActive = chkIsActive.Checked;

            var res = _supplierService.SaveSupplier(_supplier);
            if (!res.IsValid)
            {
                lblValidation.Text = string.Join("; ", res.Errors);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
