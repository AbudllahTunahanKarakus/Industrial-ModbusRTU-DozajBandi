using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks; // Asenkron işlemler için eklendi
using System.Windows; // Hata mesajı (MessageBox) gösterebilmek için eklendi
using System.Threading; // Thread.Sleep için eklendi
using System.Windows; // Hata mesajı (MessageBox) gösterebilmek için eklendi

namespace ModbusLibrary
{
    public class DatabaseManager
    {
        
        private string serverName = "TUNA";

        private string dbName = "DozajDbWPF";
        private string connectionString;

        public DatabaseManager()
        {
            // Trusted_Connection yanına 'TrustServerCertificate=True' eklendi!
            connectionString = $"Server={serverName};Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";

            
            VeritabaniniOlustur();

            
            TablolariOlustur();
        }

        // --- VERİTABANI OLUŞTURMA ---
        private void VeritabaniniOlustur()
        {
            try
            {
                
                string masterConnectionString = $"Server={serverName};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

                using (SqlConnection conn = new SqlConnection(masterConnectionString))
                {
                    conn.Open();
                    string sql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{dbName}')
                    BEGIN
                        CREATE DATABASE {dbName};
                    END";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                // SQL Server'ın veritabanı MDF dosyalarını diske yazması için 1 saniye
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                // Artık hata gizli kalmayacak, ekrana fırlayacak!
                MessageBox.Show($"Veritabanı oluşturulurken bir hata oluştu!\n\nLütfen kodun en üstündeki 'serverName' değerinin ('{serverName}') doğru SQL sunucu adı olduğundan emin olun.\n\nSistem Hatası:\n{ex.Message}", "SQL Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- TABLOLARI OLUŞTURMA  ---
        private void TablolariOlustur()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. HaberlesmeLog: Periyodik veriler
                    string sqlHaberlesme = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'HaberlesmeLog')
                    CREATE TABLE HaberlesmeLog (
                        Id INT PRIMARY KEY IDENTITY(1,1),
                        Tarih DATETIME DEFAULT GETDATE(),
                        Debi FLOAT, 
                        AnlikAgirlik FLOAT, 
                        BantHizi FLOAT,
                        ToplamAgirlik1 FLOAT, 
                        ToplamAgirlik2 FLOAT, 
                        BunkerAgirlik FLOAT,
                        Komut31 INT,
                        Komut32 INT,
                        Giris14 INT,
                        Cikis15 INT,
                        Status16 INT,
                        CalismaOturumu NVARCHAR(50) NULL
                    );";

                    // 2. OlayLog: Tüm arıza ve sistem olayları
                    string sqlOlaylar = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'OlayLog')
                    CREATE TABLE OlayLog (
                        Id INT PRIMARY KEY IDENTITY(1,1),
                        Tarih DATETIME DEFAULT GETDATE(),
                        Mesaj NVARCHAR(MAX),
                        Tip NVARCHAR(50),
                        CalismaOturumu NVARCHAR(50) NULL
                    );";

                    // 3. AyarGecmisi
                    string sqlAyarlar = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'AyarGecmisi')
                    CREATE TABLE AyarGecmisi (
                        Id INT PRIMARY KEY IDENTITY(1,1),
                        Tarih DATETIME DEFAULT GETDATE(),
                        ParametreAdi NVARCHAR(100),
                        EskiDeger NVARCHAR(100),
                        YeniDeger NVARCHAR(100),
                        CalismaOturumu NVARCHAR(50) NULL
                    );";

                    using (SqlCommand cmd = new SqlCommand(sqlHaberlesme, conn)) cmd.ExecuteNonQuery();
                    using (SqlCommand cmd = new SqlCommand(sqlOlaylar, conn)) cmd.ExecuteNonQuery();
                    using (SqlCommand cmd = new SqlCommand(sqlAyarlar, conn)) cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tablolar oluşturulurken bir hata oluştu!\n\nVeritabanı bağlantısı sağlanamadı.\n\nSistem Hatası:\n{ex.Message}", "Tablo Kurulum Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // arayüzü dondurmayan Olay Kaydı
        public void OlayKaydet(string mesaj, string tip, string oturum = "")
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string sql = "INSERT INTO OlayLog (Mesaj, Tip, Tarih, CalismaOturumu) VALUES (@mesaj, @tip, GETDATE(), @oturum)";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@mesaj", mesaj);
                            cmd.Parameters.AddWithValue("@tip", tip);
                            cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { }
            });
        }

        // arayüzü dondurmayan Periyodik Veri Kaydı
        public void VeriKaydet(double debi, double agirlik, double hiz, double t1, double t2, double bunker, int k31, int k32, int g14, int c15, int s16, string oturum = "")
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string sql = @"INSERT INTO HaberlesmeLog (Debi, AnlikAgirlik, BantHizi, ToplamAgirlik1, ToplamAgirlik2, BunkerAgirlik, Komut31, Komut32, Giris14, Cikis15, Status16, CalismaOturumu) 
                                       VALUES (@debi, @agirlik, @hiz, @t1, @t2, @bunker, @k31, @k32, @g14, @c15, @s16, @oturum)";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@debi", debi);
                            cmd.Parameters.AddWithValue("@agirlik", agirlik);
                            cmd.Parameters.AddWithValue("@hiz", hiz);
                            cmd.Parameters.AddWithValue("@t1", t1);
                            cmd.Parameters.AddWithValue("@t2", t2);
                            cmd.Parameters.AddWithValue("@bunker", bunker);
                            cmd.Parameters.AddWithValue("@k31", k31);
                            cmd.Parameters.AddWithValue("@k32", k32);
                            cmd.Parameters.AddWithValue("@g14", g14);
                            cmd.Parameters.AddWithValue("@c15", c15);
                            cmd.Parameters.AddWithValue("@s16", s16);
                            cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { }
            });
        }

        //  arayüzü dondurmayan Ayar Kaydı
        public void AyarKaydet(string param, string eski, string yeni, string oturum = "")
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string sql = "INSERT INTO AyarGecmisi (ParametreAdi, EskiDeger, YeniDeger, CalismaOturumu) VALUES (@p, @e, @y, @oturum)";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@p", param);
                            cmd.Parameters.AddWithValue("@e", eski);
                            cmd.Parameters.AddWithValue("@y", yeni);
                            cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { }
            });
        }

        // Tablo getirme işlemleri veriyi UI'a yüklediği için Task.Run İÇİNE ALINMADI.
        public DataTable GetirOlayLoglari(string oturumId = "")
        {
            DataTable dt = new DataTable();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT TOP 500 Tarih, Mesaj, Tip, CalismaOturumu FROM OlayLog ";

                    if (!string.IsNullOrEmpty(oturumId))
                    {
                        sql += "WHERE CalismaOturumu = @oturum ";
                    }

                    sql += "ORDER BY Tarih DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrEmpty(oturumId))
                            cmd.Parameters.AddWithValue("@oturum", oturumId);

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }
            }
            catch { }
            return dt;
        }

        // WPF Rapor Ekranı İçin Gelişmiş Filtreli Log Getirme Metodu
        public DataTable GetirFiltreliLoglar(string zamanFiltresi, string aramaKelimesi, string aktifOturum)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // 1=1 diyerek ardına eklenecek 'AND' komutları için zemin hazırlıyoruz
                    string sql = "SELECT TOP 2000 Tarih, Mesaj, Tip, CalismaOturumu FROM OlayLog WHERE 1=1 ";

                    // 1. Zaman ve Oturum Filtresi
                    if (zamanFiltresi == "Bu Oturum")
                        sql += "AND CalismaOturumu = @oturum ";
                    else if (zamanFiltresi == "Son 24 Saat")
                        sql += "AND Tarih >= DATEADD(day, -1, GETDATE()) ";
                    else if (zamanFiltresi == "Son 3 Gün") 
                        sql += "AND Tarih >= DATEADD(day, -3, GETDATE()) "; 
                    else if (zamanFiltresi == "Son 1 Hafta")
                        sql += "AND Tarih >= DATEADD(week, -1, GETDATE()) ";
                    else if (zamanFiltresi == "Son 1 Ay")
                        sql += "AND Tarih >= DATEADD(month, -1, GETDATE()) ";

                    // 2. Kelime Arama Filtresi (Arama kutusu boş değilse)
                    if (!string.IsNullOrWhiteSpace(aramaKelimesi))
                        sql += "AND (Mesaj LIKE @arama OR CalismaOturumu LIKE @arama) ";

                    // En son tarihe göre yeniden eskiye sıralıyoruz
                    sql += "ORDER BY Tarih DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Parametreleri güvenli bir şekilde (SQL Injection'dan korunarak) ekliyoruz
                        cmd.Parameters.AddWithValue("@oturum", aktifOturum ?? "");

                        if (!string.IsNullOrWhiteSpace(aramaKelimesi))
                            cmd.Parameters.AddWithValue("@arama", "%" + aramaKelimesi + "%");

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetirFiltreliLoglar Hatası: " + ex.Message);
            }
            return dt;
        }
    }
}
