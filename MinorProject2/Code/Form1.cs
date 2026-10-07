#nullable disable   // turning off null warnings so the code stays simple
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace MinorProject2_GUI
{
    public partial class Form1 : Form
    {
        private readonly DataAccess db = new DataAccess();
        private readonly AppSettings settings = AppSettings.Load();
        private bool isDark = false;

        // tabs
        private TabControl mainTabs;
        private TabPage tabPlayers, tabClubs, tabManagers, tabAdmin, tabSettings;

        // players tab
        private DataGridView gridPlayers;
        private TextBox txtSearchPlayer, txtPlayerFirst, txtPlayerLast, txtPlayerPos;
        private ComboBox cmbPlayerClub;

        // clubs tab (just for looking, admins change clubs)
        private DataGridView gridClubs;

        // managers tab
        private DataGridView gridManagers;
        private TextBox txtMgrFirst, txtMgrLast, txtMgrStyle;
        private ComboBox cmbMgrClub;

        // admin tab (this is where clubs get added / edited / deleted)
        private DataGridView gridAdmin;
        private TextBox txtClubName, txtClubCity, txtClubStadium;

        // theme colors (these get filled in by ApplyTheme)
        private Color themeBg, themeFg, themeBox, themeButton, themeHeader;

        // tiny class so the club dropdowns can hold a name AND an id
        private class ClubItem
        {
            public int? Id;
            public string Name;
            public override string ToString() { return Name; }
        }

        public Form1()
        {
            InitializeComponent();
            SetupAppLayout();
            RunSafely(LoadAllData);
        }

        private void SetupAppLayout()
        {
            this.Text = "Premier League Manager";
            this.Size = new Size(950, 650);
            this.MinimumSize = new Size(850, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            // owner draw so the tab headers can change color in dark mode
            mainTabs = new TabControl { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed };
            mainTabs.DrawItem += MainTabs_DrawItem;

            tabPlayers = new TabPage("Players");
            SetupPlayerTab();

            tabClubs = new TabPage("Clubs");
            SetupClubTab();

            tabManagers = new TabPage("Managers");
            SetupManagerTab();

            tabAdmin = new TabPage("Admin");
            SetupAdminTab();

            tabSettings = new TabPage("Settings");
            SetupSettingsTab();

            mainTabs.TabPages.AddRange(new[] { tabPlayers, tabClubs, tabManagers, tabAdmin, tabSettings });
            this.Controls.Add(mainTabs);

            // use whatever theme the user picked last time
            ApplyTheme(settings.DarkMode);
        }

        // paints the tab headers so they match the theme
        private void MainTabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage tabPage = mainTabs.TabPages[e.Index];

            Color backColor = isDark ? Color.FromArgb(40, 44, 52) : Color.White;
            Color foreColor = isDark ? Color.White : Color.Black;

            if (e.State == DrawItemState.Selected)
            {
                backColor = isDark ? Color.FromArgb(70, 74, 82) : Color.LightGray;
            }

            using (SolidBrush brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            using (SolidBrush brush = new SolidBrush(foreColor))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(tabPage.Text, e.Font, brush, e.Bounds, sf);
            }
        }

        // ================= PLAYERS TAB =================
        private void SetupPlayerTab()
        {
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 150 };

            // search stuff
            Label lblSearch = MakeLabel("Search by first name, last name, or position:", 15, 5);
            txtSearchPlayer = MakeTextBox(15, 32, 230, 50);
            Button btnSearch = MakeButton("Search", 255, 30, 80);
            Button btnShowAll = MakeButton("Show All", 345, 30, 80);

            btnSearch.Click += (s, e) => RunSafely(LoadPlayers);
            btnShowAll.Click += (s, e) => { txtSearchPlayer.Clear(); RunSafely(LoadPlayers); };
            txtSearchPlayer.KeyDown += (s, e) =>
            {
                // hitting enter searches too
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunSafely(LoadPlayers); }
            };

            // boxes for adding / editing a player
            Label lblF = MakeLabel("First name:", 15, 78);
            txtPlayerFirst = MakeTextBox(85, 78, 110, 50);
            Label lblL = MakeLabel("Last name:", 205, 78);
            txtPlayerLast = MakeTextBox(275, 78, 110, 50);
            Label lblP = MakeLabel("Position:", 395, 78);
            txtPlayerPos = MakeTextBox(455, 78, 100, 50);
            Label lblC = MakeLabel("Club:", 565, 78);
            cmbPlayerClub = MakeCombo(610, 78, 180);

            // buttons
            Button btnAdd = MakeButton("Add Player", 15, 112, 100);
            Button btnSave = MakeButton("Save Changes", 125, 112, 110);
            Button btnDelete = MakeButton("Delete Player", 245, 112, 110);
            Button btnClear = MakeButton("Clear Boxes", 365, 112, 100);
            Label lblTip = MakeLabel("Tip: click a player in the table to edit or delete them.", 480, 112);

            btnAdd.Click += (s, e) => AddPlayer();
            btnSave.Click += (s, e) => SavePlayer();
            btnDelete.Click += (s, e) => DeletePlayer();
            btnClear.Click += (s, e) => ClearPlayerBoxes();

            pnlTop.Controls.AddRange(new Control[] { lblSearch, txtSearchPlayer, btnSearch, btnShowAll,
                lblF, txtPlayerFirst, lblL, txtPlayerLast, lblP, txtPlayerPos, lblC, cmbPlayerClub,
                btnAdd, btnSave, btnDelete, btnClear, lblTip });

            gridPlayers = MakeGrid();
            gridPlayers.CurrentCellChanged += (s, e) => FillPlayerBoxes();

            tabPlayers.Controls.AddRange(new Control[] { gridPlayers, pnlTop });
        }

        // CREATE
        private void AddPlayer()
        {
            if (!NamesLookOk(txtPlayerFirst, txtPlayerLast)) return;

            var vals = PlayerValues();
            RunSafely(() =>
            {
                db.InsertRecord("Player", vals);
                txtSearchPlayer.Clear();   // clear the search so the new player shows up
                LoadPlayers();
                SelectLastRow(gridPlayers);
            });
        }

        // UPDATE
        private void SavePlayer()
        {
            DataRowView row = GetPickedRow(gridPlayers);
            if (row == null)
            {
                Say("Click a player in the table first, change what you want, then hit Save.");
                return;
            }
            if (!NamesLookOk(txtPlayerFirst, txtPlayerLast)) return;

            int id = (int)row["PlayerID"];
            var vals = PlayerValues();
            RunSafely(() =>
            {
                db.UpdateRecord("Player", "PlayerID", id, vals);
                LoadPlayers();
                SelectRowById(gridPlayers, "PlayerID", id);
            });
        }

        // DELETE
        private void DeletePlayer()
        {
            DataRowView row = GetPickedRow(gridPlayers);
            if (row == null)
            {
                Say("Click a player in the table first.");
                return;
            }
            if (!SureAboutDelete(row["FirstName"] + " " + row["LastName"])) return;

            int id = (int)row["PlayerID"];
            RunSafely(() =>
            {
                db.DeleteRecord("Player", "PlayerID", id);
                LoadPlayers();
            });
        }

        // puts what's in the boxes into a dictionary the data layer can use
        private Dictionary<string, object> PlayerValues()
        {
            return new Dictionary<string, object>
            {
                { "FirstName", txtPlayerFirst.Text.Trim() },
                { "LastName", txtPlayerLast.Text.Trim() },
                { "Position", OrNull(txtPlayerPos.Text) },
                { "ClubID", GetPickedClubId(cmbPlayerClub) }
            };
        }

        // when you click a row, copy it into the boxes
        private void FillPlayerBoxes()
        {
            DataRowView row = GetPickedRow(gridPlayers);
            if (row == null) { ClearPlayerBoxes(); return; }

            txtPlayerFirst.Text = row["FirstName"].ToString();
            txtPlayerLast.Text = row["LastName"].ToString();
            txtPlayerPos.Text = row["Position"].ToString();
            PickClub(cmbPlayerClub, row["ClubID"]);
        }

        private void ClearPlayerBoxes()
        {
            txtPlayerFirst.Clear();
            txtPlayerLast.Clear();
            txtPlayerPos.Clear();
            PickClub(cmbPlayerClub, null);
        }

        // ================= CLUBS TAB (just looking) =================
        private void SetupClubTab()
        {
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 45 };
            Label lblInfo = MakeLabel("Just looking here! Only admins can change clubs, so head to the Admin tab for that.", 15, 14);
            pnlTop.Controls.Add(lblInfo);

            gridClubs = MakeGrid();

            tabClubs.Controls.AddRange(new Control[] { gridClubs, pnlTop });
        }

        // ================= MANAGERS TAB =================
        private void SetupManagerTab()
        {
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 95 };

            // boxes for adding / editing a manager
            Label lblF = MakeLabel("First name:", 15, 15);
            txtMgrFirst = MakeTextBox(85, 15, 110, 50);
            Label lblL = MakeLabel("Last name:", 205, 15);
            txtMgrLast = MakeTextBox(275, 15, 110, 50);
            Label lblS = MakeLabel("Style:", 395, 15);
            txtMgrStyle = MakeTextBox(435, 15, 120, 50);
            Label lblC = MakeLabel("Club:", 565, 15);
            cmbMgrClub = MakeCombo(610, 15, 180);

            // buttons
            Button btnAdd = MakeButton("Add Manager", 15, 52, 110);
            Button btnSave = MakeButton("Save Changes", 135, 52, 110);
            Button btnDelete = MakeButton("Delete Manager", 255, 52, 120);
            Button btnClear = MakeButton("Clear Boxes", 385, 52, 100);
            Label lblTip = MakeLabel("Tip: click a manager in the table to edit or delete them.", 500, 52);

            btnAdd.Click += (s, e) => AddManager();
            btnSave.Click += (s, e) => SaveManager();
            btnDelete.Click += (s, e) => DeleteManager();
            btnClear.Click += (s, e) => ClearManagerBoxes();

            pnlTop.Controls.AddRange(new Control[] { lblF, txtMgrFirst, lblL, txtMgrLast, lblS, txtMgrStyle, lblC, cmbMgrClub,
                btnAdd, btnSave, btnDelete, btnClear, lblTip });

            gridManagers = MakeGrid();
            gridManagers.CurrentCellChanged += (s, e) => FillManagerBoxes();

            tabManagers.Controls.AddRange(new Control[] { gridManagers, pnlTop });
        }

        // CREATE
        private void AddManager()
        {
            if (!NamesLookOk(txtMgrFirst, txtMgrLast)) return;

            var vals = ManagerValues();
            RunSafely(() =>
            {
                db.InsertRecord("Manager", vals);
                LoadManagers();
                SelectLastRow(gridManagers);
            });
        }

        // UPDATE
        private void SaveManager()
        {
            DataRowView row = GetPickedRow(gridManagers);
            if (row == null)
            {
                Say("Click a manager in the table first, change what you want, then hit Save.");
                return;
            }
            if (!NamesLookOk(txtMgrFirst, txtMgrLast)) return;

            int id = (int)row["ManagerID"];
            var vals = ManagerValues();
            RunSafely(() =>
            {
                db.UpdateRecord("Manager", "ManagerID", id, vals);
                LoadManagers();
                SelectRowById(gridManagers, "ManagerID", id);
            });
        }

        // DELETE
        private void DeleteManager()
        {
            DataRowView row = GetPickedRow(gridManagers);
            if (row == null)
            {
                Say("Click a manager in the table first.");
                return;
            }
            if (!SureAboutDelete(row["FirstName"] + " " + row["LastName"])) return;

            int id = (int)row["ManagerID"];
            RunSafely(() =>
            {
                db.DeleteRecord("Manager", "ManagerID", id);
                LoadManagers();
            });
        }

        private Dictionary<string, object> ManagerValues()
        {
            return new Dictionary<string, object>
            {
                { "FirstName", txtMgrFirst.Text.Trim() },
                { "LastName", txtMgrLast.Text.Trim() },
                { "TacticalStyle", OrNull(txtMgrStyle.Text) },
                { "ClubID", GetPickedClubId(cmbMgrClub) }
            };
        }

        private void FillManagerBoxes()
        {
            DataRowView row = GetPickedRow(gridManagers);
            if (row == null) { ClearManagerBoxes(); return; }

            txtMgrFirst.Text = row["FirstName"].ToString();
            txtMgrLast.Text = row["LastName"].ToString();
            txtMgrStyle.Text = row["TacticalStyle"].ToString();
            PickClub(cmbMgrClub, row["ClubID"]);
        }

        private void ClearManagerBoxes()
        {
            txtMgrFirst.Clear();
            txtMgrLast.Clear();
            txtMgrStyle.Clear();
            PickClub(cmbMgrClub, null);
        }

        // ================= ADMIN TAB =================
        // clubs are admin only, so add / edit / delete for clubs all lives here
        private void SetupAdminTab()
        {
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 150 };

            Label lblInfo = new Label
            {
                Text = "Admin Zone: clubs can only be added, changed, or deleted from here.",
                Location = new Point(15, 12),
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true
            };
            Label lblTip = MakeLabel("Click a club in the table to edit or delete it.", 15, 38);

            // boxes for adding / editing a club
            Label lblN = MakeLabel("Club name:", 15, 78);
            txtClubName = MakeTextBox(90, 78, 150, 100);
            Label lblCity = MakeLabel("City:", 255, 78);
            txtClubCity = MakeTextBox(290, 78, 130, 100);
            Label lblStad = MakeLabel("Stadium:", 435, 78);
            txtClubStadium = MakeTextBox(495, 78, 150, 100);

            // buttons
            Button btnAdd = MakeButton("Add Club", 15, 112, 100);
            Button btnSave = MakeButton("Save Changes", 125, 112, 110);
            Button btnDelete = MakeButton("Delete Club", 245, 112, 110);
            Button btnClear = MakeButton("Clear Boxes", 365, 112, 100);

            btnAdd.Click += (s, e) => AddClub();
            btnSave.Click += (s, e) => SaveClub();
            btnDelete.Click += (s, e) => DeleteClub();
            btnClear.Click += (s, e) => ClearClubBoxes();

            pnlTop.Controls.AddRange(new Control[] { lblInfo, lblTip, lblN, txtClubName, lblCity, txtClubCity, lblStad, txtClubStadium,
                btnAdd, btnSave, btnDelete, btnClear });

            gridAdmin = MakeGrid();
            gridAdmin.CurrentCellChanged += (s, e) => FillClubBoxes();

            tabAdmin.Controls.AddRange(new Control[] { gridAdmin, pnlTop });
        }

        // CREATE
        private void AddClub()
        {
            if (txtClubName.Text.Trim() == "")
            {
                Say("The club needs a name first.");
                return;
            }

            var vals = ClubValues();
            RunSafely(() =>
            {
                db.InsertRecord("Club", vals);
                LoadAllData();   // reload everything so the club dropdowns get the new club too
                SelectLastRow(gridAdmin);
            });
        }

        // UPDATE
        private void SaveClub()
        {
            DataRowView row = GetPickedRow(gridAdmin);
            if (row == null)
            {
                Say("Click a club in the table first, change what you want, then hit Save.");
                return;
            }
            if (txtClubName.Text.Trim() == "")
            {
                Say("The club needs a name first.");
                return;
            }

            int id = (int)row["ClubID"];
            var vals = ClubValues();
            RunSafely(() =>
            {
                db.UpdateRecord("Club", "ClubID", id, vals);
                LoadAllData();
                SelectRowById(gridAdmin, "ClubID", id);
            });
        }

        // DELETE
        private void DeleteClub()
        {
            DataRowView row = GetPickedRow(gridAdmin);
            if (row == null)
            {
                Say("Click a club in the table first.");
                return;
            }
            if (!SureAboutDelete(row["ClubName"].ToString())) return;

            int id = (int)row["ClubID"];
            RunSafely(() =>
            {
                db.DeleteRecord("Club", "ClubID", id);
                LoadAllData();
            });
        }

        private Dictionary<string, object> ClubValues()
        {
            return new Dictionary<string, object>
            {
                { "ClubName", txtClubName.Text.Trim() },
                { "City", OrNull(txtClubCity.Text) },
                { "Stadium", OrNull(txtClubStadium.Text) }
            };
        }

        private void FillClubBoxes()
        {
            DataRowView row = GetPickedRow(gridAdmin);
            if (row == null) { ClearClubBoxes(); return; }

            txtClubName.Text = row["ClubName"].ToString();
            txtClubCity.Text = row["City"].ToString();
            txtClubStadium.Text = row["Stadium"].ToString();
        }

        private void ClearClubBoxes()
        {
            txtClubName.Clear();
            txtClubCity.Clear();
            txtClubStadium.Clear();
        }

        // ================= SETTINGS TAB =================
        private void SetupSettingsTab()
        {
            Panel pnl = new Panel { Dock = DockStyle.Fill };

            Label lblTheme = new Label { Text = "How should it look?", Location = new Point(25, 25), Font = new Font(Font, FontStyle.Bold), AutoSize = true };
            RadioButton radLight = new RadioButton { Text = "Light mode", Location = new Point(30, 55), AutoSize = true, Checked = !settings.DarkMode };
            RadioButton radDark = new RadioButton { Text = "Dark mode", Location = new Point(30, 85), AutoSize = true, Checked = settings.DarkMode };

            Label lblOther = new Label { Text = "Other stuff", Location = new Point(25, 135), Font = new Font(Font, FontStyle.Bold), AutoSize = true };
            CheckBox chkAskDelete = new CheckBox { Text = "Ask me before deleting things", Location = new Point(30, 165), AutoSize = true, Checked = settings.AskBeforeDelete };

            Label lblNote = new Label { Text = "Your settings save on their own, so they'll still be here next time you open the app.", Location = new Point(25, 215), AutoSize = true };

            // radLight and radDark are a pair, so only one handler is needed
            radDark.CheckedChanged += (s, e) =>
            {
                settings.DarkMode = radDark.Checked;
                settings.Save();
                ApplyTheme(settings.DarkMode);
            };

            chkAskDelete.CheckedChanged += (s, e) =>
            {
                settings.AskBeforeDelete = chkAskDelete.Checked;
                settings.Save();
            };

            pnl.Controls.AddRange(new Control[] { lblTheme, radLight, radDark, lblOther, chkAskDelete, lblNote });
            tabSettings.Controls.Add(pnl);
        }

        private void ApplyTheme(bool dark)
        {
            isDark = dark;
            themeBg = dark ? Color.FromArgb(40, 44, 52) : Color.White;
            themeFg = dark ? Color.White : Color.Black;
            themeBox = dark ? Color.FromArgb(50, 54, 62) : Color.White;
            themeButton = dark ? Color.FromArgb(70, 74, 82) : Color.LightGray;
            themeHeader = dark ? Color.FromArgb(50, 54, 62) : Color.LightGray;

            this.BackColor = themeBg;
            this.ForeColor = themeFg;

            foreach (TabPage tab in mainTabs.TabPages)
            {
                tab.BackColor = themeBg;
                tab.ForeColor = themeFg;
            }

            mainTabs.Invalidate();   // makes the tab headers redraw
            UpdateControlColors(this);
        }

        // goes through every control and colors it
        private void UpdateControlColors(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is DataGridView)
                {
                    DataGridView grid = (DataGridView)c;
                    grid.BackgroundColor = themeBg;
                    grid.DefaultCellStyle.BackColor = themeBg;
                    grid.DefaultCellStyle.ForeColor = themeFg;
                    grid.GridColor = Color.Gray;

                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = themeHeader;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = themeFg;
                    grid.RowHeadersDefaultCellStyle.BackColor = themeHeader;
                    grid.RowHeadersDefaultCellStyle.ForeColor = themeFg;
                }
                else if (c is TextBox || c is ComboBox)
                {
                    c.BackColor = themeBox;
                    c.ForeColor = themeFg;

                    if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                    if (c is ComboBox) ((ComboBox)c).FlatStyle = FlatStyle.Flat;
                }
                else if (c is Button)
                {
                    Button btn = (Button)c;
                    btn.BackColor = themeButton;
                    btn.ForeColor = themeFg;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.Gray;
                }

                if (c.HasChildren)
                {
                    UpdateControlColors(c);
                }
            }
        }

        // ================= LOADING DATA =================
        private void LoadPlayers()
        {
            // if there's something in the search box, only show matches
            string keyword = txtSearchPlayer.Text.Trim();
            if (keyword == "") gridPlayers.DataSource = db.GetPlayers();
            else gridPlayers.DataSource = db.SearchPlayers(keyword);

            TidyGrid(gridPlayers, true);
        }

        private void LoadManagers()
        {
            gridManagers.DataSource = db.GetManagers();
            TidyGrid(gridManagers, true);
        }

        private void LoadClubs()
        {
            // two separate tables so clicking in one doesn't move the other
            gridClubs.DataSource = db.GetAllRecords("Club");
            gridAdmin.DataSource = db.GetAllRecords("Club");
            TidyGrid(gridClubs, false);
            TidyGrid(gridAdmin, false);
        }

        // fills the club dropdowns on the Players and Managers tabs
        private void LoadClubDropdowns()
        {
            DataTable clubs = db.GetAllRecords("Club");

            foreach (ComboBox box in new[] { cmbPlayerClub, cmbMgrClub })
            {
                box.Items.Clear();
                box.Items.Add(new ClubItem { Id = null, Name = "(no club)" });
                foreach (DataRow row in clubs.Rows)
                {
                    box.Items.Add(new ClubItem { Id = (int)row["ClubID"], Name = row["ClubName"].ToString() });
                }
                box.SelectedIndex = 0;
            }
        }

        private void LoadAllData()
        {
            LoadClubDropdowns();   // do this first, the other tables need the dropdowns ready
            LoadPlayers();
            LoadClubs();
            LoadManagers();
        }

        // ================= HELPERS =================
        // runs database stuff and shows a friendly message instead of crashing if something goes wrong
        private void RunSafely(Action doStuff)
        {
            try
            {
                doStuff();
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                // 547 means a foreign key problem (like deleting a club that still has players)
                MessageBox.Show("Can't do that one yet. It's still connected to other stuff, like a club that still has players or managers. Move or delete those first, then try again.",
                    "Hold up", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong with the database:\n\n" + ex.Message + "\n\nIs SQL Server running? Did you run the .sql script?",
                    "Oops", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Say(string message)
        {
            MessageBox.Show(message, "Heads up", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // asks "you sure?" before deleting (unless they turned that off in Settings)
        private bool SureAboutDelete(string what)
        {
            if (!settings.AskBeforeDelete) return true;

            DialogResult answer = MessageBox.Show("Really delete " + what + "? You can't undo this one.",
                "Hold up", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return answer == DialogResult.Yes;
        }

        // first and last name can't be empty
        private bool NamesLookOk(TextBox first, TextBox last)
        {
            if (first.Text.Trim() == "" || last.Text.Trim() == "")
            {
                Say("Add a first name and a last name first.");
                return false;
            }
            return true;
        }

        // empty box = NULL in the database
        private object OrNull(string text)
        {
            text = text.Trim();
            return text == "" ? (object)DBNull.Value : text;
        }

        // gets whatever row is clicked in a table (or null if nothing is)
        private DataRowView GetPickedRow(DataGridView grid)
        {
            if (grid.CurrentRow == null) return null;
            return grid.CurrentRow.DataBoundItem as DataRowView;
        }

        // gets the club id from a club dropdown (NULL if "(no club)")
        private object GetPickedClubId(ComboBox box)
        {
            ClubItem club = box.SelectedItem as ClubItem;
            if (club == null || club.Id == null) return DBNull.Value;
            return club.Id.Value;
        }

        // sets a club dropdown to match a club id
        private void PickClub(ComboBox box, object clubId)
        {
            if (box.Items.Count == 0) return;

            box.SelectedIndex = 0;   // "(no club)" unless we find a match
            if (clubId == null || clubId is DBNull) return;

            for (int i = 0; i < box.Items.Count; i++)
            {
                if (((ClubItem)box.Items[i]).Id == (int)clubId)
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
        }

        // clicks the row with this id (so the same one stays picked after a reload)
        private void SelectRowById(DataGridView grid, string idColumn, int id)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                DataRowView item = row.DataBoundItem as DataRowView;
                if (item != null && (int)item[idColumn] == id)
                {
                    grid.CurrentCell = row.Cells[0];
                    return;
                }
            }
        }

        // clicks the last row (that's where a new record lands)
        private void SelectLastRow(DataGridView grid)
        {
            if (grid.Rows.Count == 0) return;
            grid.CurrentCell = grid.Rows[grid.Rows.Count - 1].Cells[0];
        }

        // makes the headers look nicer ("FirstName" turns into "First Name") and can hide the club id number
        private void TidyGrid(DataGridView grid, bool hideClubId)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.HeaderText = Regex.Replace(col.Name, "(?<=[a-z])(?=[A-Z])", " ");
            }

            if (hideClubId && grid.Columns.Contains("ClubID"))
            {
                grid.Columns["ClubID"].Visible = false;
            }
        }

        // quick builders so we aren't typing the same stuff over and over
        private Label MakeLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y + 3), AutoSize = true };
        }

        private TextBox MakeTextBox(int x, int y, int width, int maxLength)
        {
            return new TextBox { Location = new Point(x, y), Width = width, MaxLength = maxLength };
        }

        private ComboBox MakeCombo(int x, int y, int width)
        {
            return new ComboBox { Location = new Point(x, y), Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
        }

        private Button MakeButton(string text, int x, int y, int width)
        {
            return new Button { Text = text, Location = new Point(x, y), Width = width, Height = 28 };
        }

        private DataGridView MakeGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
        }
    }
}