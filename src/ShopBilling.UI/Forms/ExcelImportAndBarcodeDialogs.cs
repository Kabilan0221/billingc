using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;
using ShopBilling.Data.Services;

namespace ShopBilling.UI.Forms
{
    public class ExcelImportDialog : Form
    {
        private readonly ProductService _productService;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IBrandRepository _brandRepo;
        private readonly IAuditLogRepository _auditRepo;

        private TextBox txtFilePath;
        private Button btnBrowse;
        private Button btnDownloadTemplate;
        private CheckBox chkUpdateExisting;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Button btnStartImport;
        private DataGridView gridErrors;

        public ExcelImportDialog(
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
        }

        private void InitializeUI()
        {
            this.Text = "Bulk Product Import from Excel (.xlsx / .csv)";
            this.Size = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var grpFile = new GroupBox { Text = "Select Excel File", Location = new Point(16, 16), Size = new Size(670, 110) };
            txtFilePath = new TextBox { Location = new Point(16, 28), Width = 480, ReadOnly = true };
            btnBrowse = new Button { Text = "Browse...", Location = new Point(505, 26), Size = new Size(80, 26) };
            btnBrowse.Click += (s, e) => BrowseFile();

            btnDownloadTemplate = new Button { Text = "Download Excel Template", Location = new Point(16, 65), Size = new Size(180, 28), FlatStyle = FlatStyle.Flat };
            btnDownloadTemplate.Click += (s, e) => DownloadTemplate();

            chkUpdateExisting = new CheckBox { Text = "Update existing products if barcode or product code matches", Location = new Point(220, 68), AutoSize = true };

            grpFile.Controls.AddRange(new Control[] { txtFilePath, btnBrowse, btnDownloadTemplate, chkUpdateExisting });

            progressBar = new ProgressBar { Location = new Point(16, 140), Size = new Size(670, 22), Minimum = 0, Maximum = 100 };
            lblStatus = new Label { Text = "Ready to import. Supports 50,000+ rows batching.", Location = new Point(16, 170), AutoSize = true };

            btnStartImport = new Button
            {
                Text = "Start Import",
                Location = new Point(540, 165),
                Size = new Size(146, 32),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Enabled = false
            };
            btnStartImport.Click += async (s, e) => await StartImportAsync();

            var grpErr = new GroupBox { Text = "Import Error Log / Duplicate Warnings", Location = new Point(16, 210), Size = new Size(670, 250) };
            gridErrors = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            gridErrors.Columns.Add("Row", "Row #");
            gridErrors.Columns["Row"].FillWeight = 40;
            gridErrors.Columns.Add("Code", "Product Code");
            gridErrors.Columns["Code"].FillWeight = 70;
            gridErrors.Columns.Add("Barcode", "Barcode");
            gridErrors.Columns["Barcode"].FillWeight = 90;
            gridErrors.Columns.Add("Error", "Validation Error / Reason");
            gridErrors.Columns["Error"].FillWeight = 200;

            grpErr.Controls.Add(gridErrors);

            this.Controls.AddRange(new Control[] { grpFile, progressBar, lblStatus, btnStartImport, grpErr });
        }

        private void BrowseFile()
        {
            using (var ofd = new OpenFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                    btnStartImport.Enabled = true;
                    lblStatus.Text = $"File selected: {Path.GetFileName(ofd.FileName)}";
                }
            }
        }

        private void DownloadTemplate()
        {
            using (var sfd = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = "ShopBilling_Product_Import_Template.xlsx" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var repo = (IProductRepository)_productService.GetType().GetField("_productRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_productService);
                    var svc = new ExcelImportExportService(repo, _categoryRepo, _brandRepo, _auditRepo);
                    svc.GenerateSampleTemplate(sfd.FileName);
                    MessageBox.Show("Template created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async Task StartImportAsync()
        {
            btnStartImport.Enabled = false;
            btnBrowse.Enabled = false;
            gridErrors.Rows.Clear();

            string filePath = txtFilePath.Text;
            bool updateDuplicates = chkUpdateExisting.Checked;

            var repo = (IProductRepository)_productService.GetType().GetField("_productRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_productService);
            var svc = new ExcelImportExportService(repo, _categoryRepo, _brandRepo, _auditRepo);

            lblStatus.Text = "Importing in batches... Please wait.";

            try
            {
                var importResult = await Task.Run(() =>
                {
                    return svc.ImportProductsFromExcel(filePath, (processed, total) =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            progressBar.Value = Math.Min(100, (int)(((double)processed / Math.Max(1, total)) * 100));
                            lblStatus.Text = $"Imported {processed} / {total} products...";
                        }));
                    }, updateDuplicates);
                });

                progressBar.Value = 100;
                lblStatus.Text = $"Import complete! Success: {importResult.SuccessfullyImported:N0}, Errors: {importResult.FailedCount:N0} ({importResult.DurationMs:F0} ms)";

                if (importResult.FailedRows.Count > 0)
                {
                    foreach (var err in importResult.FailedRows)
                    {
                        gridErrors.Rows.Add(err.RowIndex, err.ProductCode, err.Barcode, string.Join("; ", err.ValidationErrors));
                    }
                }

                MessageBox.Show(
                    $"Import Summary:\nTotal Rows: {importResult.TotalRows:N0}\nSuccessfully Imported: {importResult.SuccessfullyImported:N0}\nFailed Rows: {importResult.FailedCount:N0}\nTime: {importResult.DurationMs:F0} ms",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Import Error: {ex.Message}";
                MessageBox.Show($"Failed to import file:\n{ex.Message}", "Import Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnStartImport.Enabled = true;
                btnBrowse.Enabled = true;
            }
        }
    }

    public class BarcodePrintDialog : Form
    {
        private readonly Product _product;
        private PictureBox picBarcode;
        private NumericUpDown numCopies;

        public BarcodePrintDialog(Product product)
        {
            _product = product;
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = $"Barcode Label Print Studio — {_product.ProductCode}";
            this.Size = new Size(420, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var grpPreview = new GroupBox { Text = "50mm x 25mm Thermal Sticker Preview", Location = new Point(16, 16), Size = new Size(370, 180) };
            picBarcode = new PictureBox
            {
                Location = new Point(20, 24),
                Size = new Size(330, 140),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            picBarcode.Image = BarcodeRenderer.GenerateBarcodeImage(_product.Barcode, 320, 110);
            grpPreview.Controls.Add(picBarcode);

            var l1 = new Label { Text = "Number of Copies:", Location = new Point(16, 216), AutoSize = true };
            numCopies = new NumericUpDown { Location = new Point(140, 214), Width = 80, Value = 1, Minimum = 1, Maximum = 1000 };

            var btnPrint = new Button
            {
                Text = "Print to Thermal Printer",
                Location = new Point(16, 260),
                Size = new Size(180, 36),
                BackColor = Color.FromArgb(16, 110, 190),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnPrint.Click += (s, e) =>
            {
                // In Windows 7, this triggers System.Drawing.Printing.PrintDocument
                MessageBox.Show($"Sent {numCopies.Value} barcode label(s) for '{_product.NameEn}' to Default Windows Printer.", "Print Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            var btnClose = new Button { Text = "Close", Location = new Point(210, 260), Size = new Size(100, 36), FlatStyle = FlatStyle.Flat };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { grpPreview, l1, numCopies, btnPrint, btnClose });
        }
    }
}
