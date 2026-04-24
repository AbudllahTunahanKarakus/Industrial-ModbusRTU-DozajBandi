using System;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ModbusLibrary;

namespace WPF
{
    public partial class RaporWindow : Window
    {
        private DatabaseManager db;
        private string mevcutOturum;

        public RaporWindow(string oturumId)
        {
            InitializeComponent();

            db = new DatabaseManager();
            mevcutOturum = oturumId;

            this.Loaded += (s, e) => VerileriYukle();
        }

        // --- VERİLERİ YÜKLEME VE FİLTRELEME ---
        private void VerileriYukle()
        {
            if (cmbZamanFiltresi.SelectedItem == null) return;

            string zamanFiltresi = (cmbZamanFiltresi.SelectedItem as ComboBoxItem).Content.ToString();
            string aramaKelimesi = txtArama.Text;

            DataTable dt = db.GetirFiltreliLoglar(zamanFiltresi, aramaKelimesi, mevcutOturum);
            dgvAlarmlar.ItemsSource = dt.DefaultView;

            lblDurum.Text = $"Listelenen Kayıt Sayısı: {dt.Rows.Count}";
        }

        // --- DİNAMİK SATIR RENKLENDİRME  ---
        private void dgvAlarmlar_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                string mesaj = rowView["Mesaj"]?.ToString() ?? "";
                string tip = rowView["Tip"]?.ToString() ?? "";

                // BAŞLADI = Kırmızı
                if (mesaj.Contains("BAŞLADI") || tip == "HATA")
                {
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(224, 108, 117)); // #E06C75 (Kırmızı)
                }
                // GİDERİLDİ = Yeşil
                else if (mesaj.Contains("GİDERİLDİ"))
                {
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(152, 195, 121)); // #98C379 (Yeşil)
                }
                // Standart Alarm = Sarı
                else if (tip == "ALARM")
                {
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(229, 192, 123)); // #E5C07B (Sarı)
                }
                else
                {
                    e.Row.Foreground = Brushes.White;
                }
            }
        }

        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            VerileriYukle();
        }

        private void cmbZamanFiltresi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                VerileriYukle();
            }
        }

        private void btnYenile_Click(object sender, RoutedEventArgs e)
        {
            VerileriYukle();
        }

        // --- EXCEL (CSV) DIŞA AKTARMA ---
        private void btnExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgvAlarmlar.Items.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak kayıt bulunamadı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Uyumlu CSV Dosyası (*.csv)|*.csv",
                FileName = $"Dozaj_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Raporu Excel Formatında Kaydet"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    DataView dv = dgvAlarmlar.ItemsSource as DataView;

                    sb.AppendLine("Olusma Zamani;Olay Aciklamasi;Kategori;Oturum ID");

                    foreach (DataRowView rowView in dv)
                    {
                        DataRow row = rowView.Row;

                        string tarih = Convert.ToDateTime(row["Tarih"]).ToString("dd.MM.yyyy HH:mm:ss");
                        string mesaj = row["Mesaj"]?.ToString().Replace(";", ",") ?? "";
                        string tip = row["Tip"]?.ToString() ?? "";
                        string oturum = row["CalismaOturumu"]?.ToString() ?? "";

                        sb.AppendLine($"{tarih};{mesaj};{tip};{oturum}");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Rapor başarıyla Excel formatında kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
