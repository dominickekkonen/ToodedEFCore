using Microsoft.EntityFrameworkCore;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Tooded_EFCore_Mihhailenko
{
    public partial class Form2 : Form
    {
        private readonly ToodedContext _db;
        private string valitudPilt = "";

        public Form2()
        {
            InitializeComponent();
            Directory.CreateDirectory("Images");   // pildikaust luuakse alati
            _db = new ToodedContext();
            LaeKategooriad();
            LaeTooted();
        }

        // ---------- Andmete laadimine ----------

        private void LaeKategooriad()
        {
            comboKategooria.DataSource = _db.Kategooriatabel.ToList();
            comboKategooria.DisplayMember = "Kategooria_nimetus";
            comboKategooria.ValueMember = "Id";
        }

        private void LaeTooted()
        {
            dataGridViewTooted.DataSource = _db.Toodetabel
                .Include(t => t.Kategooria)
                .Select(t => new
                {
                    t.Id,
                    t.Toodenimetus,
                    t.Kogus,
                    t.Hind,
                    Kategooria = t.Kategooria.Kategooria_nimetus,
                    t.Pilt
                })
                .ToList();
        }

        // ---------- Abimeetodid ----------

        // Näitab pilti nii, et faili ei jää lukku (tehakse koopia mällu)
        private void NaitaPilti(string tee)
        {
            picturePilt.Image?.Dispose();
            picturePilt.Image = null;

            if (!string.IsNullOrEmpty(tee) && File.Exists(tee))
            {
                using var fs = new FileStream(tee, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = Image.FromStream(fs);
                picturePilt.Image = new Bitmap(img);
            }
        }

        // Kopeerib pildi Images kausta (ei kopeeri faili iseenda peale)
        private void KopeeriPilt(string allikas, string failinimi)
        {
            Directory.CreateDirectory("Images");
            string sihtTee = Path.Combine("Images", failinimi);

            if (!string.Equals(Path.GetFullPath(allikas),
                               Path.GetFullPath(sihtTee),
                               StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(allikas, sihtTee, true);
            }
        }

        // Kontrollib sisestatud andmed. Kui midagi on valesti, näitab teate ja tagastab false
        private bool KontrolliAndmed(out string nimetus, out int kogus, out float hind, out int kategooriaId)
        {
            nimetus = "";
            kogus = 0;
            hind = 0;
            kategooriaId = 0;

            if (string.IsNullOrWhiteSpace(txtNimetus.Text) ||
                string.IsNullOrWhiteSpace(txtKogus.Text) ||
                string.IsNullOrWhiteSpace(txtHind.Text))
            {
                MessageBox.Show("Palun täida kõik väljad: toote nimetus, kogus ja hind!",
                                "Puuduvad andmed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!int.TryParse(txtKogus.Text.Trim(), out kogus) || kogus < 0)
            {
                MessageBox.Show("Kogus peab olema täisarv (nt 10).",
                                "Vigane kogus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtKogus.Focus();
                return false;
            }

            // Lubab nii 2,50 kui ka 2.50
            string hindTekst = txtHind.Text.Trim().Replace(',', '.');
            if (!float.TryParse(hindTekst, NumberStyles.Float, CultureInfo.InvariantCulture, out hind) || hind < 0)
            {
                MessageBox.Show("Hind peab olema number (nt 2,50).",
                                "Vigane hind", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtHind.Focus();
                return false;
            }

            if (comboKategooria.SelectedValue == null)
            {
                MessageBox.Show("Palun vali kategooria!",
                                "Kategooria puudub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            nimetus = txtNimetus.Text.Trim();
            kategooriaId = (int)comboKategooria.SelectedValue;
            return true;
        }

        // Tagastab tabelis valitud toote Id või null, kui midagi pole valitud
        private int? ValitudToodeId()
        {
            DataGridViewRow rida = null;

            if (dataGridViewTooted.SelectedRows.Count > 0)
                rida = dataGridViewTooted.SelectedRows[0];
            else if (dataGridViewTooted.CurrentRow != null)
                rida = dataGridViewTooted.CurrentRow;

            if (rida == null || rida.Cells["Id"].Value == null)
                return null;

            return (int)rida.Cells["Id"].Value;
        }

        private void TuhjendaVaated()
        {
            txtNimetus.Clear();
            txtKogus.Clear();
            txtHind.Clear();
            picturePilt.Image?.Dispose();
            picturePilt.Image = null;
            valitudPilt = "";
        }

        // ---------- Nupud ----------

        private void btnValiPilt_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Pildid|*.jpg;*.png;*.jpeg";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                valitudPilt = ofd.FileName;
                NaitaPilti(valitudPilt);
            }
        }

        private void btnLisa_Click(object sender, EventArgs e)
        {
            if (!KontrolliAndmed(out string nimetus, out int kogus, out float hind, out int kategooriaId))
                return;

            // Pilt on andmebaasis "not null", seega peab olema valitud
            if (string.IsNullOrEmpty(valitudPilt))
            {
                MessageBox.Show("Palun vali toote pilt!",
                                "Pilt puudub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var uus = new Toode
            {
                Toodenimetus = nimetus,
                Kogus = kogus,
                Hind = hind,
                KategooriaId = kategooriaId,
                Pilt = Path.GetFileName(valitudPilt)
            };

            KopeeriPilt(valitudPilt, uus.Pilt);

            _db.Toodetabel.Add(uus);
            _db.SaveChanges();
            LaeTooted();

            TuhjendaVaated();
        }

        private void dataGridViewTooted_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var rida = dataGridViewTooted.Rows[e.RowIndex];

            txtNimetus.Text = rida.Cells["Toodenimetus"].Value?.ToString() ?? "";
            txtKogus.Text = rida.Cells["Kogus"].Value?.ToString() ?? "";
            txtHind.Text = rida.Cells["Hind"].Value?.ToString() ?? "";

            // uus rida valitud -> vana valitud pilt ei kehti enam
            valitudPilt = "";

            string pilt = rida.Cells["Pilt"].Value?.ToString() ?? "";
            NaitaPilti(Path.Combine("Images", pilt));
        }

        private void btnUuenda_Click(object sender, EventArgs e)
        {
            int? id = ValitudToodeId();
            if (id == null)
            {
                MessageBox.Show("Vali tabelist toode, mida soovid uuendada!",
                                "Toode valimata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!KontrolliAndmed(out string nimetus, out int kogus, out float hind, out int kategooriaId))
                return;

            var toode = _db.Toodetabel.Find(id.Value);
            if (toode == null)
            {
                MessageBox.Show("Toodet ei leitud andmebaasist.",
                                "Viga", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            toode.Toodenimetus = nimetus;
            toode.Kogus = kogus;
            toode.Hind = hind;
            toode.KategooriaId = kategooriaId;

            if (!string.IsNullOrEmpty(valitudPilt))
            {
                toode.Pilt = Path.GetFileName(valitudPilt);
                KopeeriPilt(valitudPilt, toode.Pilt);
            }

            _db.SaveChanges();
            LaeTooted();

            valitudPilt = "";
        }

        private void btnKustuta_Click(object sender, EventArgs e)
        {
            int? id = ValitudToodeId();
            if (id == null)
            {
                MessageBox.Show("Vali tabelist toode, mida soovid kustutada!",
                                "Toode valimata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var toode = _db.Toodetabel.Find(id.Value);
            if (toode == null) return;

            var vastus = MessageBox.Show($"Kas oled kindel, et soovid toote \"{toode.Toodenimetus}\" kustutada?",
                                         "Kustutamine", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (vastus != DialogResult.Yes) return;

            _db.Toodetabel.Remove(toode);
            _db.SaveChanges();
            LaeTooted();

            TuhjendaVaated();
        }

        private void btnArve_Click(object sender, EventArgs e)
        {
            int? id = ValitudToodeId();
            if (id == null) return;

            var toode = _db.Toodetabel.Find(id.Value);
            if (toode == null) return;

            ArveForm arve = new ArveForm();
            arve.SelectedToode = toode;
            arve.ShowDialog();
        }
    }
}
