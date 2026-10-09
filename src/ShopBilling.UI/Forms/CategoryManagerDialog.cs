using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.UI.Forms
{
    public class CategoryManagerDialog : Form
    {
        private readonly ICategoryRepository _categoryRepo;
        private ListBox lstCategories;
        private ListBox lstSubcategories;
        private TextBox txtCatName;
        private TextBox txtCatCode;
        private TextBox txtSubName;
        private TextBox txtSubCode;
        private Category selectedCat;

        public CategoryManagerDialog(ICategoryRepository categoryRepo)
        {
            _categoryRepo = categoryRepo;
            InitializeUI();
            LoadCategories();
        }

        private void InitializeUI()
        {
            this.Text = "Category & Subcategory Hierarchy Management";
            this.Size = new Size(680, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Categories Column
            var grpCat = new GroupBox { Text = "Categories", Location = new Point(16, 16), Size = new Size(310, 440) };
            lstCategories = new ListBox { Location = new Point(12, 24), Size = new Size(284, 280) };
            lstCategories.SelectedIndexChanged += (s, e) => OnCategorySelected();

            var lblCName = new Label { Text = "Category Name:", Location = new Point(12, 314), AutoSize = true };
            txtCatName = new TextBox { Location = new Point(12, 334), Width = 180 };
            txtCatCode = new TextBox { Location = new Point(200, 334), Width = 96 };

            var btnAddCat = new Button { Text = "+ Add Category", Location = new Point(12, 368), Size = new Size(136, 30), BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAddCat.Click += (s, e) => AddCategory();

            var btnDeleteCat = new Button { Text = "Deactivate", Location = new Point(160, 368), Size = new Size(136, 30), FlatStyle = FlatStyle.Flat };
            btnDeleteCat.Click += (s, e) => DeleteCategory();

            grpCat.Controls.AddRange(new Control[] { lstCategories, lblCName, txtCatName, txtCatCode, btnAddCat, btnDeleteCat });

            // Subcategories Column
            var grpSub = new GroupBox { Text = "Subcategories (Belongs to Selected Category)", Location = new Point(340, 16), Size = new Size(310, 440) };
            lstSubcategories = new ListBox { Location = new Point(12, 24), Size = new Size(284, 280) };

            var lblSName = new Label { Text = "Subcategory Name:", Location = new Point(12, 314), AutoSize = true };
            txtSubName = new TextBox { Location = new Point(12, 334), Width = 180 };
            txtSubCode = new TextBox { Location = new Point(200, 334), Width = 96 };

            var btnAddSub = new Button { Text = "+ Add Subcategory", Location = new Point(12, 368), Size = new Size(140, 30), BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAddSub.Click += (s, e) => AddSubcategory();

            grpSub.Controls.AddRange(new Control[] { lstSubcategories, lblSName, txtSubName, txtSubCode, btnAddSub });

            this.Controls.Add(grpSub);
            this.Controls.Add(grpCat);
        }

        private void LoadCategories()
        {
            var cats = _categoryRepo.GetAll().ToList();
            lstCategories.DataSource = cats;
            lstCategories.DisplayMember = "Name";
            lstCategories.ValueMember = "Id";
        }

        private void OnCategorySelected()
        {
            selectedCat = lstCategories.SelectedItem as Category;
            if (selectedCat != null)
            {
                var subs = _categoryRepo.GetSubcategories(selectedCat.Id).ToList();
                lstSubcategories.DataSource = subs;
                lstSubcategories.DisplayMember = "Name";
                lstSubcategories.ValueMember = "Id";
            }
        }

        private void AddCategory()
        {
            if (string.IsNullOrWhiteSpace(txtCatName.Text)) return;
            string code = string.IsNullOrWhiteSpace(txtCatCode.Text) ? "CAT-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() : txtCatCode.Text.Trim();
            _categoryRepo.Insert(new Category { Name = txtCatName.Text.Trim(), Code = code });
            txtCatName.Text = "";
            txtCatCode.Text = "";
            LoadCategories();
        }

        private void DeleteCategory()
        {
            if (selectedCat != null)
            {
                _categoryRepo.Delete(selectedCat.Id);
                LoadCategories();
            }
        }

        private void AddSubcategory()
        {
            if (selectedCat == null || string.IsNullOrWhiteSpace(txtSubName.Text)) return;
            string code = string.IsNullOrWhiteSpace(txtSubCode.Text) ? "SUB-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() : txtSubCode.Text.Trim();
            _categoryRepo.InsertSubcategory(new Subcategory { CategoryId = selectedCat.Id, Name = txtSubName.Text.Trim(), Code = code });
            txtSubName.Text = "";
            txtSubCode.Text = "";
            OnCategorySelected();
        }
    }

    public class BrandManagerDialog : Form
    {
        private readonly IBrandRepository _brandRepo;
        private ListBox lstBrands;
        private TextBox txtName;
        private TextBox txtCode;

        public BrandManagerDialog(IBrandRepository brandRepo)
        {
            _brandRepo = brandRepo;
            InitializeUI();
            LoadBrands();
        }

        private void InitializeUI()
        {
            this.Text = "Brand Master";
            this.Size = new Size(420, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            lstBrands = new ListBox { Location = new Point(16, 16), Size = new Size(370, 300) };

            var l1 = new Label { Text = "Brand Name:", Location = new Point(16, 326), AutoSize = true };
            txtName = new TextBox { Location = new Point(16, 346), Width = 230 };

            var l2 = new Label { Text = "Code:", Location = new Point(256, 326), AutoSize = true };
            txtCode = new TextBox { Location = new Point(256, 346), Width = 130 };

            var btnAdd = new Button { Text = "+ Add Brand", Location = new Point(16, 384), Size = new Size(180, 32), BackColor = Color.FromArgb(16, 110, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAdd.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtName.Text))
                {
                    string code = string.IsNullOrWhiteSpace(txtCode.Text) ? "BR-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() : txtCode.Text.Trim();
                    _brandRepo.Insert(new Brand { Name = txtName.Text.Trim(), Code = code });
                    txtName.Text = "";
                    txtCode.Text = "";
                    LoadBrands();
                }
            };

            var btnDelete = new Button { Text = "Deactivate", Location = new Point(206, 384), Size = new Size(180, 32), FlatStyle = FlatStyle.Flat };
            btnDelete.Click += (s, e) =>
            {
                if (lstBrands.SelectedItem is Brand b)
                {
                    _brandRepo.Delete(b.Id);
                    LoadBrands();
                }
            };

            this.Controls.AddRange(new Control[] { lstBrands, l1, txtName, l2, txtCode, btnAdd, btnDelete });
        }

        private void LoadBrands()
        {
            lstBrands.DataSource = _brandRepo.GetAll().ToList();
            lstBrands.DisplayMember = "Name";
            lstBrands.ValueMember = "Id";
        }
    }
}
