using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks; // Asenkron işlemler için eklendi

namespace ModbusLibrary
{
    public class DatabaseManager
    {
        // SQL Server bağlantı dizesi
        private string connectionString = "Server=TUNA;Database=DozajDB;Trusted_Connection=True;";

        public DatabaseManager()
        {
            TablolariOlustur();
        }

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
                System.Diagnostics.Debug.WriteLine("Veritabanı Başlatma Hatası: " + ex.Message);
            }
        }

        //  arayüzü dondurmayan Olay Kaydı
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("OlayKaydet Hatası: " + ex.Message);
                }
            });
            
        }

        //  arayüzü dondurmayan Periyodik Veri Kaydı
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("VeriKaydet Hatası: " + ex.Message);
                }
            });
        }

        // mantığı ile arayüzü dondurmayan Ayar Kaydı
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("AyarKaydet Hatası: " + ex.Message);
                }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetirOlayLoglari Hatası: " + ex.Message);
            }
            return dt;
        }
    }
}



//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.SqlClient;

//namespace ModbusLibrary
//{
//    public class DatabaseManager
//    {
//        // SQL Server bağlantı dizesi
//        private string connectionString = "Server=TUNA;Database=DozajDB;Trusted_Connection=True;";

//        public DatabaseManager()
//        {
//            TablolariOlustur();
//        }

//        private void TablolariOlustur()
//        {
//            try
//            {
//                using (SqlConnection conn = new SqlConnection(connectionString))
//                {
//                    conn.Open();

//                    // 1. HaberlesmeLog: Periyodik veriler
//                    string sqlHaberlesme = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'HaberlesmeLog')
//                    CREATE TABLE HaberlesmeLog (
//                        Id INT PRIMARY KEY IDENTITY(1,1),
//                        Tarih DATETIME DEFAULT GETDATE(),
//                        Debi FLOAT, 
//                        AnlikAgirlik FLOAT, 
//                        BantHizi FLOAT,
//                        ToplamAgirlik1 FLOAT, 
//                        ToplamAgirlik2 FLOAT, 
//                        BunkerAgirlik FLOAT,
//                        Komut31 INT,
//                        Komut32 INT,
//                        Giris14 INT,
//                        Cikis15 INT,
//                        Status16 INT
//                    );";

//                    // 2. OlayLog: Tüm arıza ve sistem olayları
//                    // NOT: Her olay yeni satır olacağı için BitisZamani sütununa artık teknik olarak gerek yok 
//                    // ama tablo yapısını bozmamak için bırakabiliriz.
//                    string sqlOlaylar = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'OlayLog')
//                    CREATE TABLE OlayLog (
//                        Id INT PRIMARY KEY IDENTITY(1,1),
//                        Tarih DATETIME DEFAULT GETDATE(),
//                        Mesaj NVARCHAR(MAX),
//                        Tip NVARCHAR(50) -- 'ALARM', 'BILGI', 'HATA'
//                    );";

//                    // 3. AyarGecmisi
//                    string sqlAyarlar = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'AyarGecmisi')
//                    CREATE TABLE AyarGecmisi (
//                        Id INT PRIMARY KEY IDENTITY(1,1),
//                        Tarih DATETIME DEFAULT GETDATE(),
//                        ParametreAdi NVARCHAR(100),
//                        EskiDeger NVARCHAR(100),
//                        YeniDeger NVARCHAR(100)
//                    );";

//                    using (SqlCommand cmd = new SqlCommand(sqlHaberlesme, conn)) cmd.ExecuteNonQuery();
//                    using (SqlCommand cmd = new SqlCommand(sqlOlaylar, conn)) cmd.ExecuteNonQuery();
//                    using (SqlCommand cmd = new SqlCommand(sqlAyarlar, conn)) cmd.ExecuteNonQuery();
//                }
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine("Veritabanı Başlatma Hatası: " + ex.Message);
//            }
//        }

//        // 1. Olay Kaydetme (Arıza, Başlangıç, Bitiş vs.)
//        public void OlayKaydet(string mesaj, string tip, string oturum)
//        {
//            try
//            {
//                using (SqlConnection conn = new SqlConnection(connectionString))
//                {
//                    conn.Open();
//                    // CalismaOturumu eklendi
//                    string sql = "INSERT INTO OlayLog (Mesaj, Tip, Tarih, CalismaOturumu) VALUES (@mesaj, @tip, GETDATE(), @oturum)";
//                    using (SqlCommand cmd = new SqlCommand(sql, conn))
//                    {
//                        cmd.Parameters.AddWithValue("@mesaj", mesaj);
//                        cmd.Parameters.AddWithValue("@tip", tip);
//                        cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
//                        cmd.ExecuteNonQuery();
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine("OlayKaydet Hatası: " + ex.Message);
//            }
//        }

//        // 2. Periyodik Veri Kaydetme (Debi, Ağırlık vb.)
//        public void VeriKaydet(double debi, double agirlik, double hiz, double t1, double t2, double bunker, int k31, int k32, int g14, int c15, int s16, string oturum)
//        {
//            try
//            {
//                using (SqlConnection conn = new SqlConnection(connectionString))
//                {
//                    conn.Open();
//                    // CalismaOturumu eklendi
//                    string sql = @"INSERT INTO HaberlesmeLog (Debi, AnlikAgirlik, BantHizi, ToplamAgirlik1, ToplamAgirlik2, BunkerAgirlik, Komut31, Komut32, Giris14, Cikis15, Status16, CalismaOturumu) 
//                                   VALUES (@debi, @agirlik, @hiz, @t1, @t2, @bunker, @k31, @k32, @g14, @c15, @s16, @oturum)";

//                    using (SqlCommand cmd = new SqlCommand(sql, conn))
//                    {
//                        cmd.Parameters.AddWithValue("@debi", debi);
//                        cmd.Parameters.AddWithValue("@agirlik", agirlik);
//                        cmd.Parameters.AddWithValue("@hiz", hiz);
//                        cmd.Parameters.AddWithValue("@t1", t1);
//                        cmd.Parameters.AddWithValue("@t2", t2);
//                        cmd.Parameters.AddWithValue("@bunker", bunker);
//                        cmd.Parameters.AddWithValue("@k31", k31);
//                        cmd.Parameters.AddWithValue("@k32", k32);
//                        cmd.Parameters.AddWithValue("@g14", g14);
//                        cmd.Parameters.AddWithValue("@c15", c15);
//                        cmd.Parameters.AddWithValue("@s16", s16);
//                        cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
//                        cmd.ExecuteNonQuery();
//                    }
//                }
//            }
//            catch { }
//        }

//        // 3. Ayar Değişikliklerini Kaydetme
//        public void AyarKaydet(string param, string eski, string yeni, string oturum)
//        {
//            try
//            {
//                using (SqlConnection conn = new SqlConnection(connectionString))
//                {
//                    conn.Open();
//                    // CalismaOturumu eklendi
//                    string sql = "INSERT INTO AyarGecmisi (ParametreAdi, EskiDeger, YeniDeger, CalismaOturumu) VALUES (@p, @e, @y, @oturum)";
//                    using (SqlCommand cmd = new SqlCommand(sql, conn))
//                    {
//                        cmd.Parameters.AddWithValue("@p", param);
//                        cmd.Parameters.AddWithValue("@e", eski);
//                        cmd.Parameters.AddWithValue("@y", yeni);
//                        cmd.Parameters.AddWithValue("@oturum", string.IsNullOrEmpty(oturum) ? (object)DBNull.Value : oturum);
//                        cmd.ExecuteNonQuery();
//                    }
//                }
//            }
//            catch { }
//        }

//       
//        public DataTable GetirOlayLoglari(string oturumId = "")
//        {
//            DataTable dt = new DataTable();
//            try
//            {
//                using (SqlConnection conn = new SqlConnection(connectionString))
//                {
//                    conn.Open();
//                    // Sadece belirli bir oturumu getirmek istersek WHERE kullanıyoruz
//                    // Eğer oturumId boş gönderilirse, geçmişteki tüm alarmları (son 500 kayıt) getirir
//                    string sql = "SELECT TOP 500 Tarih, Mesaj, Tip, CalismaOturumu FROM OlayLog ";

//                    if (!string.IsNullOrEmpty(oturumId))
//                    {
//                        sql += "WHERE CalismaOturumu = @oturum ";
//                    }

//                    sql += "ORDER BY Tarih DESC"; // En yeniler en üstte görünsün

//                    using (SqlCommand cmd = new SqlCommand(sql, conn))
//                    {
//                        if (!string.IsNullOrEmpty(oturumId))
//                            cmd.Parameters.AddWithValue("@oturum", oturumId);

//                        // SqlDataAdapter, SQL'den gelen veriyi otomatik olarak DataTable'a doldurur
//                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
//                        {
//                            da.Fill(dt);
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine("GetirOlayLoglari Hatası: " + ex.Message);
//            }
//            return dt;
//        }
//    }
//}
