using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusLibrary
{
    public partial class DozajDashboard : Form
    {
        // Temel Haberleşme Nesneleri
        SerialPortManager.SerialPortManager serial;
        ModbusMaster.ModbusMaster master;

        // Hafıza Değişkenleri
        ushort eskiInV = 0, eskiInO = 0, eskiInS = 0, eskiBnkrSt = 0;
        ushort bnkrSt = 0;

        // Kontrol Bayrakları
        bool isHaberlesmeActive = false;
        bool isWriting = false;

        // TextBox Değişiklik Takibi
        bool is30Changed = false, is33Changed = false, is34Changed = false;

        // Komut Hafızaları
        ushort komutVerisi31 = 0;
        ushort gonderilecekKomut32 = 0;

        // Hata takip sayacı
        int consecutiveErrorCount = 0;

        // Database Nesnesi
        DatabaseManager db = new DatabaseManager();

        //  OTURUM KİMLİĞİ DEĞİŞKENİ BURADA
        public string aktifOturum = "";

        public DozajDashboard()
        {
            InitializeComponent();
            ArayuzuSifirla();

            Timer timerDbSave = new Timer();
            timerDbSave.Interval = 60000; // 1 Dakika
            timerDbSave.Tick += (s, e) => PeriyodikVeriKaydet();
            timerDbSave.Start();
        }

        private async Task HaberlesmeDongusu()
        {
            while (isHaberlesmeActive)
            {
                if (master != null && master.IsOpen() && !isWriting)
                {
                    try
                    {
                        byte slaveID = (byte)numSlaveId.Value;
                        byte[] data = await master.ReadHoldingRegistersAsync(slaveID, 0, 35);

                        if (data != null && data.Length >= 73)
                        {
                            consecutiveErrorCount = 0;

                            this.Invoke((MethodInvoker)delegate
                            {
                                VerileriEkranaDagit(data);
                                if (lblCanlılık.ForeColor == Color.Black) lblCanlılık.ForeColor = Color.Green;
                                else lblCanlılık.ForeColor = Color.Black;
                            });
                        }
                        else { consecutiveErrorCount++; }
                    }
                    catch
                    {
                        consecutiveErrorCount++;
                    }

                    if (consecutiveErrorCount >= 100)
                    {
                        this.Invoke((MethodInvoker)delegate {
                            if (consecutiveErrorCount == 100)
                            {
                                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] !!! BAĞLANTI KOPTU !!!");

                                //  KOPMA DURUMUNDA AKTİF OTURUM KİMLİĞİYLE KAYIT ATILIR
                                db.OlayKaydet("Haberleşme zaman aşımı: 100 ardışık hata oluştu.", "ALARM", aktifOturum);

                                ArayuzuSifirla();
                            }
                        });
                    }
                }
                await Task.Delay(100);
            }
        }

        //  Nokta/Virgül hatalarını çözen evrensel dönüştürücü
        private double ParseDoubleSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0;
            string cleaned = val.Replace(" kg", "").Replace("%", "").Replace(",", ".");
            double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }

        //  UI Titremesini (Flicker) ve İşlemci Yükünü Önleyen Yardımcı Metotlar
        private void SetText(Label lbl, string text)
        {
            if (lbl.Text != text) lbl.Text = text;
        }

        private void SetText(TextBox txt, string text)
        {
            if (txt.Text != text) txt.Text = text;
        }

        private void SetLamba(Label lbl, bool isAktif, Color aktifRenk)
        {
            Color yeniRenk = isAktif ? aktifRenk : SystemColors.MenuBar;
            if (lbl.BackColor != yeniRenk) lbl.BackColor = yeniRenk;
        }

        private void SetButtonColor(Button btn, bool isAktif, Color aktifRenk, Color pasifRenk)
        {
            Color yeniRenk = isAktif ? aktifRenk : pasifRenk;
            if (btn.BackColor != yeniRenk) btn.BackColor = yeniRenk;
        }

        private void PeriyodikVeriKaydet()
        {
            if (!isHaberlesmeActive) return;

            try
            {
                double debi = ParseDoubleSafe(lblFlowRate.Text);
                double agirlik = ParseDoubleSafe(lblAnlikAgirlik.Text);
                double hiz = ParseDoubleSafe(lblBeltSpeed.Text);
                double t1 = ParseDoubleSafe(lblTotalWeight1.Text);
                double t2 = ParseDoubleSafe(lblTotalWeight2.Text);
                double bunker = ParseDoubleSafe(lblBunkerAgirlik.Text);

                int k31 = komutVerisi31;
                int k32 = gonderilecekKomut32;

                db.VeriKaydet(debi, agirlik, hiz, t1, t2, bunker, k31, k32, eskiInV, eskiInO, eskiInS, aktifOturum);
            }
            catch { }
        }

        private void VerileriEkranaDagit(byte[] data)
        {
            ushort GetReg(int addr) => (ushort)((data[3 + (addr * 2)] << 8) | data[3 + (addr * 2) + 1]);

            try
            {
                // Ana Sayısal Göstergeler
                SetText(lblSetDegeri, GetReg(1).ToString());
                SetText(lblAnlikAgirlik, GetReg(4).ToString() + " kg");
                SetText(lblBeltSpeed, "%" + GetReg(5).ToString());
                SetText(lblControlOut, GetReg(6).ToString());
                SetText(lblAgirlikSet, GetReg(11).ToString() + " kg");
                SetText(lblGecenToplam, GetReg(12).ToString() + " kg");
                SetText(lblOnBesleyiciCikis, GetReg(13).ToString());

                // Ondalıklı Debi
                int dp = GetReg(17);
                double bolen = Math.Pow(10, dp);
                SetText(lblFlowRate, (GetReg(3) / bolen).ToString("F" + dp));
                SetText(lblOrtalamaDebi, (GetReg(2) / bolen).ToString("F" + dp));
                SetText(lblNoktanınYeri, dp.ToString());

                // 32-Bit Toplayıcılar
                SetText(lblTotalWeight1, (GetReg(7) | (GetReg(8) << 16)).ToString() + " kg");
                SetText(lblTotalWeight2, (GetReg(9) | (GetReg(10) << 16)).ToString() + " kg");

                // Giriş Lambaları
                ushort inV = GetReg(14);
                SetLamba(lblGiris0, (inV & 0x0001) != 0, Color.Green);
                SetLamba(lblGiris1, (inV & 0x0002) != 0, Color.Green);
                SetLamba(lblGiris2, (inV & 0x0004) != 0, Color.Green);
                SetLamba(lblGiris3, (inV & 0x0008) != 0, Color.Red);
                SetLamba(lblGiris4, (inV & 0x0010) != 0, Color.Green);
                SetLamba(lblGiris5, (inV & 0x0020) != 0, Color.Red);
                SetLamba(lblGiris6, (inV & 0x0040) != 0, Color.Green);
                SetLamba(lblGiris7, (inV & 0x0080) != 0, Color.Green);
                SetLamba(lblGiris8, (inV & 0x0100) != 0, Color.Green);
                SetLamba(lblGiris9, (inV & 0x0200) != 0, Color.Green);
                SetLamba(lblGiris12, (inV & 0x1000) != 0, Color.Green);
                SetLamba(lblGiris13, (inV & 0x2000) != 0, Color.Green);
                SetLamba(lblGiris14, (inV & 0x4000) != 0, Color.Red);
                SetLamba(lblGiris15, (inV & 0x8000) != 0, Color.Red);

                // Çıkış Lambaları
                ushort inO = GetReg(15);
                SetLamba(lblCikis0, (inO & 0x0001) != 0, Color.Red);
                SetLamba(lblCikis1, (inO & 0x0002) != 0, Color.Red);
                SetLamba(lblCikis2, (inO & 0x0004) != 0, Color.Red);
                SetLamba(lblCikis3, (inO & 0x0008) != 0, Color.Red);
                SetLamba(lblCikis4, (inO & 0x0010) != 0, Color.Red);
                SetLamba(lblCikis5, (inO & 0x0020) != 0, Color.Red);
                SetLamba(lblCikis6, (inO & 0x0040) != 0, Color.Green);
                SetLamba(lblCikis7, (inO & 0x0080) != 0, Color.Green);
                SetLamba(lblCikis9, (inO & 0x0200) != 0, Color.Red);
                SetLamba(lblCikis10, (inO & 0x0400) != 0, Color.Yellow);
                SetLamba(lblCikis11, (inO & 0x0800) != 0, Color.Green);
                SetLamba(lblCikis12, (inO & 0x1000) != 0, Color.Green);
                SetLamba(lblCikis13, (inO & 0x2000) != 0, Color.Red);
                SetLamba(lblCikis14, (inO & 0x4000) != 0, Color.Green);
                SetLamba(lblCikis15, (inO & 0x8000) != 0, Color.Green);

                // Status Lambaları
                ushort inS = GetReg(16);
                SetLamba(lblStatus0, (inS & 0x0001) != 0, Color.Green);
                SetLamba(lblStatus1, (inS & 0x0002) != 0, Color.Green);
                SetLamba(lblStatus2, (inS & 0x0004) != 0, Color.Green);
                SetLamba(lblStatus3, (inS & 0x0008) != 0, Color.Red);
                SetLamba(lblStatus4, (inS & 0x0010) != 0, Color.Green);
                SetLamba(lblStatus5, (inS & 0x0020) != 0, Color.Red);
                SetLamba(lblStatus6, (inS & 0x0040) != 0, Color.Green);
                SetLamba(lblStatus7, (inS & 0x0080) != 0, Color.Green);
                SetLamba(lblStatus8, (inS & 0x0100) != 0, Color.Green);
                SetLamba(lblStatus9, (inS & 0x0200) != 0, Color.Green);
                SetLamba(lblStatus10, (inS & 0x0400) != 0, Color.Red);
                SetLamba(lblStatus11, (inS & 0x0800) != 0, Color.Red);
                SetLamba(lblStatus12, (inS & 0x1000) != 0, Color.Green);
                SetLamba(lblStatus13, (inS & 0x2000) != 0, Color.Green);

                // Bunker Verileri
                if (chkBunkerMevcut.Checked)
                {
                    SetText(lblBunkerAgirlik, GetReg(18).ToString());
                    SetText(lblBunkerAgirlikSet, GetReg(19).ToString());
                    SetText(lblBunkerAgirlikKontrolCikis, GetReg(20).ToString());
                    SetText(lblBunkerBosalanAgirlik, GetReg(22).ToString() + " kg");
                    SetText(lblBandTarttigiAgirlik, GetReg(23).ToString() + " kg");

                    bnkrSt = GetReg(21);
                    SetLamba(lblKalibrasyonIsleminde, (bnkrSt & 0x0001) != 0, Color.Green);
                    SetLamba(lblKalibrasyonToleransIcinde, (bnkrSt & 0x0002) != 0, Color.Green);
                    SetLamba(lblKalibrasyonToleransDisinda, (bnkrSt & 0x0004) != 0, Color.Green);
                    SetLamba(lblBunkerAgırlikAriza, (bnkrSt & 0x0008) != 0, Color.Red);
                    SetLamba(lblbunkerSeviyesiAltSeviyeAltinda, (bnkrSt & 0x0010) != 0, Color.Green);
                    SetLamba(lblBunkerSeviyeUstSeviyeUstunde, (bnkrSt & 0x0020) != 0, Color.Green);
                }

                // Textbox Güncellemeleri
                if (!isWriting)
                {
                    if (!is30Changed && !txtTargetDose.Focused) SetText(txtTargetDose, GetReg(30).ToString());
                    if (!is33Changed && !txtAgirlikSet.Focused) SetText(txtAgirlikSet, GetReg(33).ToString());
                    if (chkBunkerMevcut.Checked && !is34Changed && !txtBnkrAgirlikSetDeger.Focused)
                    {
                        SetText(txtBnkrAgirlikSetDeger, GetReg(34).ToString());
                    }

                    komutVerisi31 = GetReg(31);
                    gonderilecekKomut32 = GetReg(32);
                }

                // Buton Renkleri
                SetButtonColor(btnCalis, (komutVerisi31 & 0x0001) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnOnSistemCalis, (komutVerisi31 & 0x0002) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnMotorCalis, (komutVerisi31 & 0x0004) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnMotorAriza, (komutVerisi31 & 0x0008) != 0, Color.Red, Color.Gray);
                SetButtonColor(btnOnBesleyiciCalis, (komutVerisi31 & 0x0010) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnOnBesleyiciAriza, (komutVerisi31 & 0x0020) != 0, Color.Red, Color.Gray);
                SetButtonColor(btnUzakCalisma, (komutVerisi31 & 0x0040) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnYakinCalisma, (komutVerisi31 & 0x0080) != 0, Color.Green, Color.Gray);

                SetButtonColor(btnTotalSifirla, (gonderilecekKomut32 & 0x0001) != 0, Color.Orange, Color.Gray);
                SetButtonColor(btnArizaSil, (gonderilecekKomut32 & 0x0002) != 0, Color.Orange, Color.Gray);
                SetButtonColor(btnKalibrasyonBasla, (gonderilecekKomut32 & 0x0010) != 0, Color.Yellow, Color.Gray);
                SetButtonColor(btnKalibrasyonKabul, (gonderilecekKomut32 & 0x0020) != 0, Color.Green, Color.Gray);
                SetButtonColor(btnKalibrasyonRed, (gonderilecekKomut32 & 0x0040) != 0, Color.Red, Color.Gray);
                SetButtonColor(btnDolumDurdur, (gonderilecekKomut32 & 0x0080) != 0, Color.Red, Color.Gray);

                ArizaKontrolVeKayit(inV, inO, inS, bnkrSt);
            }
            catch { }
        }

        private async Task GuvenliYaz(ushort adres, ushort deger)
        {
            try
            {
                isWriting = true;
                await Task.Delay(50);
                await master.WriteHoldingRegisterAsync((byte)numSlaveId.Value, adres, deger);
            }
            finally
            {
                isWriting = false;
            }
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            // Bağlantıyı güvenli bir şekilde kapat
            if (isHaberlesmeActive)
            {
                isHaberlesmeActive = false;
                await Task.Delay(250);
                if (serial != null) serial.Close();

                //  MANUEL KESİNTİDE OTURUM KAYDI ATILIR
                db.OlayKaydet("Kullanıcı bağlantıyı manuel olarak kesti.", "BILGI", aktifOturum);

                //  OTURUM KİMLİĞİ SIFIRLANIR
                aktifOturum = "";

                ArayuzuSifirla();

                btnConnect.Text = "Bağlan";
                btnConnect.BackColor = Color.LightGreen;
                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Bağlantı Kesildi.");
                return;
            }

            try
            {
                serial = new SerialPortManager.SerialPortManager(cmbPort.Text, int.Parse(cmbBaudRate.Text), System.IO.Ports.Parity.Even, 8, System.IO.Ports.StopBits.One);
                master = new ModbusMaster.ModbusMaster(serial);
                serial.Open();

                if (serial.IsOpen())
                {
                    isHaberlesmeActive = true;
                    consecutiveErrorCount = 0;

                    //  YENİ OTURUM KİMLİĞİ OLUŞTURULUR
                    aktifOturum = "RUN-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

                    btnConnect.Text = "Bağlantıyı Kes";
                    btnConnect.BackColor = Color.LightPink;

                    // BAŞLANGIÇ LOGU AKTİF OTURUM KİMLİĞİYLE ATILIR
                    db.OlayKaydet($"{cmbPort.Text} portu üzerinden bağlantı sağlandı.", "BILGI", aktifOturum);

                    _ = HaberlesmeDongusu();
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {cmbPort.Text} Başarıyla Bağlandı.");
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Oturum Başladı: {aktifOturum}");
                    ButonlariAktiflestir();
                }
                else
                {
                    throw new Exception("Port fiziksel olarak açılamadı!");
                }
            }
            catch (Exception ex)
            {
                isHaberlesmeActive = false;
                aktifOturum = ""; // Hata varsa oturumu temizle
                btnConnect.Text = "Bağlan";
                btnConnect.BackColor = Color.LightGreen;
                MessageBox.Show($"Bağlantı Hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- KOMUT 1 BUTONLARI (Register 31) ---
        private async void btnCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 1; await GuvenliYaz(31, komutVerisi31); }
        private async void btnOnSistemCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 2; await GuvenliYaz(31, komutVerisi31); }
        private async void btnMotorCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 4; await GuvenliYaz(31, komutVerisi31); }
        private async void btnMotorAriza_Click(object sender, EventArgs e) { komutVerisi31 ^= 8; await GuvenliYaz(31, komutVerisi31); }
        private async void btnOnBesleyiciCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 16; await GuvenliYaz(31, komutVerisi31); }
        private async void btnOnBesleyiciAriza_Click(object sender, EventArgs e) { komutVerisi31 ^= 32; await GuvenliYaz(31, komutVerisi31); }
        private async void btnUzakCalisma_Click(object sender, EventArgs e) { komutVerisi31 ^= 64; await GuvenliYaz(31, komutVerisi31); }
        private async void btnYakinCalisma_Click(object sender, EventArgs e) { komutVerisi31 ^= 128; await GuvenliYaz(31, komutVerisi31); }

        // --- KOMUT 2 BUTONLARI (Register 32) ---

        //   Yaylı (Pulse) Buton - Toplayıcı Sıfırla
        private async void btnTotalSifirla_Click(object sender, EventArgs e)
        {
            // 1. İlgili biti SET et (1 yap)
            gonderilecekKomut32 |= 1;
            await GuvenliYaz(32, gonderilecekKomut32);

            // 2. PLC'nin komutu yakalaması için kısa bir süre (500ms) bekle
            await Task.Delay(500);

            // 3. İlgili biti RESET et (0 yap) - Hata giderildi: Tam kapsamlı explicit dönüştürme uygulandı.
            gonderilecekKomut32 = (ushort)(gonderilecekKomut32 & ~1);
            await GuvenliYaz(32, gonderilecekKomut32);
        }

        //  Yaylı (Pulse) Buton - Arıza Sil
        private async void btnArizaSil_Click(object sender, EventArgs e)
        {
            // 1. İlgili biti SET et (1 yap)
            gonderilecekKomut32 |= 2;
            await GuvenliYaz(32, gonderilecekKomut32);

            // 2. PLC'nin komutu yakalaması için bekle
            await Task.Delay(500);

            // 3. İlgili biti RESET et (0 yap) - Hata giderildi.
            gonderilecekKomut32 = (ushort)(gonderilecekKomut32 & ~2);
            await GuvenliYaz(32, gonderilecekKomut32);
        }

        // 4 ve 8 Kullanılmıyor 
        private async void btnKalibrasyonBasla_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 16; await GuvenliYaz(32, gonderilecekKomut32); }
        private async void btnKalibrasyonKabul_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 32; await GuvenliYaz(32, gonderilecekKomut32); }
        private async void btnKalibrasyonRed_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 64; await GuvenliYaz(32, gonderilecekKomut32); }
        private async void btnDolumDurdur_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 128; await GuvenliYaz(32, gonderilecekKomut32); }

        private async void btnAyarlar_Click(object sender, EventArgs e)
        {
            if (!isHaberlesmeActive || master == null)
            {
                MessageBox.Show("Lütfen önce cihazla bağlantı kurun!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

                    try
                    {
                        if (ushort.TryParse(txtTargetDose.Text, out ushort v30))
                        {
                            string eskiDeger = lblSetDegeri.Text;
                            await GuvenliYaz(30, v30);
                            // AYARLAR KAYDEDİLİRKEN AKTİF OTURUM GÖNDERİLİR
                            db.AyarKaydet("Hedef Dozaj", eskiDeger, v30.ToString(), aktifOturum);
                        }

                        if (ushort.TryParse(txtAgirlikSet.Text, out ushort v33))
                        {
                            string eskiDeger = lblAgirlikSet.Text.Replace(" kg", "");
                            await GuvenliYaz(33, v33);
                            db.AyarKaydet("Ağırlık Set", eskiDeger, v33.ToString(), aktifOturum);
                        }

                        if (chkBunkerMevcut.Checked && groupBunker.Enabled)
                        {
                            if (ushort.TryParse(txtBnkrAgirlikSetDeger.Text, out ushort v34))
                            {
                                string eskiDeger = lblBunkerAgirlikSet.Text;
                                await GuvenliYaz(34, v34);
                                db.AyarKaydet("Bunker Ağırlık Set", eskiDeger, v34.ToString(), aktifOturum);
                            }
                        }

                        is30Changed = is33Changed = is34Changed = false;
                        lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Ayarlar başarıyla güncellendi.");
                    }
                    catch (Exception ex)
                    {
                        db.OlayKaydet($"Ayar yazma hatası: {ex.Message}", "HATA", aktifOturum);
                    }
                }


        //  Rapor formunu açan buton tıklama olayı
        private void btnRaporlar_Click(object sender, EventArgs e)
        {
            // Yeni oluşturduğumuz formu, o anki aktif oturum ID'si ile başlatıyoruz
            RaporForm raporEkrani = new RaporForm(aktifOturum);

            // ShowDialog ile açarsak, kullanıcı bu pencereyi kapatana kadar 
            
            raporEkrani.ShowDialog();
        }

        private void txtTargetDose_TextChanged(object sender, EventArgs e) { if (txtTargetDose.Focused) is30Changed = true; }
        private void txtAgirlikSet_TextChanged(object sender, EventArgs e) { if (txtAgirlikSet.Focused) is33Changed = true; }
        private void txtBnkrAgirlikSetDeger_TextChanged(object sender, EventArgs e) { if (txtBnkrAgirlikSetDeger.Focused) is34Changed = true; }

        private void chkBunkerMevcut_CheckedChanged(object sender, EventArgs e)
        {
            bool isActive = chkBunkerMevcut.Checked;
            groupBunker.Enabled = isActive;
            groupBunkerStatus.Enabled = isActive;
            txtBnkrAgirlikSetDeger.Enabled = isActive;
            txtBnkrAgirlikSetDeger.BackColor = isActive ? SystemColors.Window : Color.Silver;

            if (isActive)
                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm}] Bunker sistemi aktif.");
            else
            {
                lblBunkerAgirlik.Text = "0";
                lblBunkerAgirlikSet.Text = "0";
                lblBunkerAgirlikKontrolCikis.Text = "0";
                lblBunkerBosalanAgirlik.Text = "0";
                lblBandTarttigiAgirlik.Text = "0";
                lblKalibrasyonIsleminde.BackColor = SystemColors.MenuBar;
                lblKalibrasyonToleransIcinde.BackColor = SystemColors.MenuBar;
                lblKalibrasyonToleransDisinda.BackColor = SystemColors.MenuBar;
                lblBunkerAgırlikAriza.BackColor = SystemColors.MenuBar;
                lblbunkerSeviyesiAltSeviyeAltinda.BackColor = SystemColors.MenuBar;
                lblBunkerSeviyeUstSeviyeUstunde.BackColor = SystemColors.MenuBar;
                lblKullanılmıyor1.BackColor = SystemColors.MenuBar;
                lblKullanılmıyo2.BackColor = SystemColors.MenuBar;
                txtBnkrAgirlikSetDeger.Text = "0";
                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm}] Bunker sistemi pasif modda.");
            }
        }

        private void DozajDashboard_Load(object sender, EventArgs e)
        {
            groupBunker.Enabled = false;
            groupBunkerStatus.Enabled = false;
            txtBnkrAgirlikSetDeger.Enabled = false;
        }

        private void ArayuzuSifirla()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(ArayuzuSifirla));
                return;
            }

            lblSetDegeri.Text = "0";
            lblAnlikAgirlik.Text = "0 kg";
            lblFlowRate.Text = "0.0";
            lblOrtalamaDebi.Text = "0.0";
            lblBeltSpeed.Text = "%0";
            lblControlOut.Text = "0";
            lblTotalWeight1.Text = "0 kg";
            lblTotalWeight2.Text = "0 kg";
            lblGecenToplam.Text = "0 kg";
            lblAgirlikSet.Text = "0";
            lblOnBesleyiciCikis.Text = "0";
            lblNoktanınYeri.Text = "0";

            txtAgirlikSet.Text = "0";
            txtBnkrAgirlikSetDeger.Text = "0";
            txtTargetDose.Text = "0";

            lblBunkerAgirlik.Text = "0";
            lblBunkerAgirlikSet.Text = "0";
            lblBunkerAgirlikKontrolCikis.Text = "0";
            lblBunkerBosalanAgirlik.Text = "0";
            lblBandTarttigiAgirlik.Text = "0";

            btnCalis.Enabled = false;
            btnOnSistemCalis.Enabled = false;
            btnMotorCalis.Enabled = false;
            btnMotorAriza.Enabled = false;
            btnOnBesleyiciCalis.Enabled = false;
            btnOnBesleyiciAriza.Enabled = false;
            btnUzakCalisma.Enabled = false;
            btnYakinCalisma.Enabled = false;
            btnTotalSifirla.Enabled = false;
            btnArizaSil.Enabled = false;
            btnKullanılmıyor1.Enabled = false;
            btnKullanılmıyor2.Enabled = false;
            btnKalibrasyonBasla.Enabled = false;
            btnKalibrasyonKabul.Enabled = false;
            btnKalibrasyonRed.Enabled = false;
            btnDolumDurdur.Enabled = false;

            btnCalis.BackColor = Color.Gray;
            btnOnSistemCalis.BackColor = Color.Gray;
            btnMotorCalis.BackColor = Color.Gray;
            btnMotorAriza.BackColor = Color.Gray;
            btnOnBesleyiciCalis.BackColor = Color.Gray;
            btnOnBesleyiciAriza.BackColor = Color.Gray;
            btnUzakCalisma.BackColor = Color.Gray;
            btnYakinCalisma.BackColor = Color.Gray;
            btnTotalSifirla.BackColor = Color.Gray;
            btnArizaSil.BackColor = Color.Gray;
            btnKullanılmıyor1.ForeColor = Color.Silver;
            btnKullanılmıyor2.ForeColor = Color.Silver;
            btnKalibrasyonBasla.BackColor = Color.Gray;
            btnKalibrasyonKabul.BackColor = Color.Gray;
            btnKalibrasyonRed.BackColor = Color.Gray;
            btnDolumDurdur.BackColor = Color.Gray;

            foreach (Control c in this.Controls)
            {
                if (c is GroupBox)
                {
                    foreach (Control gc in c.Controls)
                    {
                        if (gc is Label && (gc.Name.StartsWith("lblGiris") || gc.Name.StartsWith("lblCikis") || gc.Name.StartsWith("lblStatus")))
                        {
                            gc.BackColor = SystemColors.MenuBar;
                        }
                    }
                }
            }

            lblKalibrasyonIsleminde.BackColor = SystemColors.MenuBar;
            lblKalibrasyonToleransIcinde.BackColor = SystemColors.MenuBar;
            lblKalibrasyonToleransDisinda.BackColor = SystemColors.MenuBar;
            lblBunkerAgırlikAriza.BackColor = SystemColors.MenuBar;
            lblbunkerSeviyesiAltSeviyeAltinda.BackColor = SystemColors.MenuBar;
            lblBunkerSeviyeUstSeviyeUstunde.BackColor = SystemColors.MenuBar;
            lblKullanılmıyor1.BackColor = SystemColors.MenuBar;
            lblKullanılmıyo2.BackColor = SystemColors.MenuBar;

            komutVerisi31 = 0;
            gonderilecekKomut32 = 0;
            lblSetDegeri.ForeColor = Color.Black;

            groupBunker.Enabled = false;
            groupBunkerStatus.Enabled = false;
            txtBnkrAgirlikSetDeger.Enabled = false;
            txtBnkrAgirlikSetDeger.BackColor = Color.Silver;
        }

        private void ButonlariAktiflestir()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(ButonlariAktiflestir));
                return;
            }

            btnCalis.Enabled = true;
            btnOnSistemCalis.Enabled = true;
            btnMotorCalis.Enabled = true;
            btnMotorAriza.Enabled = true;
            btnOnBesleyiciCalis.Enabled = true;
            btnOnBesleyiciAriza.Enabled = true;
            btnUzakCalisma.Enabled = true;
            btnYakinCalisma.Enabled = true;
            btnTotalSifirla.Enabled = true;
            btnArizaSil.Enabled = true;
            btnKullanılmıyor1.Enabled = true;
            btnKullanılmıyor2.Enabled = true;
            btnKalibrasyonBasla.Enabled = true;
            btnKalibrasyonKabul.Enabled = true;
            btnKalibrasyonRed.Enabled = true;
            btnDolumDurdur.Enabled = true;

            btnCalis.BackColor = Color.Gray;
            btnOnSistemCalis.BackColor = Color.Gray;
            btnMotorCalis.BackColor = Color.Gray;
            btnMotorAriza.BackColor = Color.Gray;
            btnOnBesleyiciCalis.BackColor = Color.Gray;
            btnOnBesleyiciAriza.BackColor = Color.Gray;
            btnUzakCalisma.BackColor = Color.Gray;
            btnYakinCalisma.BackColor = Color.Gray;
            btnTotalSifirla.BackColor = Color.Gray;
            btnArizaSil.BackColor = Color.Gray;
            btnKalibrasyonBasla.BackColor = Color.Gray;
            btnKalibrasyonKabul.BackColor = Color.Gray;
            btnKalibrasyonRed.BackColor = Color.Gray;
            btnDolumDurdur.BackColor = Color.Gray;

            btnKullanılmıyor1.ForeColor = Color.Black;
            btnKullanılmıyor2.ForeColor = Color.Black;
        }

        private void ArizaKontrolVeKayit(ushort inV, ushort inO, ushort inS, ushort bnkrSt)
        {
            CheckAndLog(inS, eskiInS, 0x0010, "Band Kaydı Arıza");
            CheckAndLog(inS, eskiInS, 0x0020, "Acil Dur Durumu");
            CheckAndLog(inS, eskiInS, 0x0040, "Ağırlık Arıza");
            CheckAndLog(inS, eskiInS, 0x0080, "Sıfır Arıza");
            CheckAndLog(inS, eskiInS, 0x0100, "Hız Arıza");
            CheckAndLog(inS, eskiInS, 0x0200, "Debi Arıza");
            CheckAndLog(inS, eskiInS, 0x0400, "Normal Çalışma Engel");
            CheckAndLog(inS, eskiInS, 0x0800, "Tolerans Arıza");
            CheckAndLog(inS, eskiInS, 0x2000, "Motor Arıza");
            CheckAndLog(bnkrSt, eskiBnkrSt, 0x0008, "Bunker Ağırlık Arızası");

            eskiInV = inV; eskiInO = inO; eskiInS = inS; eskiBnkrSt = bnkrSt;
        }

        // CheckAndLog METODU DA AKTİF OTURUM KİMLİĞİYLE KAYIT ATAR
        private void CheckAndLog(ushort current, ushort old, ushort mask, string arizaAdi)
        {
            bool suAnHataVar = (current & mask) != 0;
            bool oncedenHataVarmiydi = (old & mask) != 0;

            if (suAnHataVar && !oncedenHataVarmiydi)
            {
                // HATA BAŞLADI (Update kalmadı, sözlük kullanılmıyor)
                db.OlayKaydet(arizaAdi + " BAŞLADI", "ALARM", aktifOturum);

                this.Invoke((MethodInvoker)delegate {
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {arizaAdi} Başladı.");
                });
            }
            else if (!suAnHataVar && oncedenHataVarmiydi)
            {
                // HATA GİDERİLDİ (Yeni satır atılır)
                db.OlayKaydet(arizaAdi + " GİDERİLDİ", "ALARM", aktifOturum);

                this.Invoke((MethodInvoker)delegate {
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {arizaAdi} Giderildi.");
                });
            }
        }
    }
}





















//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using Microsoft.Win32;

//namespace ModbusLibrary
//{
//    public partial class DozajDashboard : Form
//    {
//        // Temel Haberleşme Nesneleri
//        SerialPortManager.SerialPortManager serial;
//        ModbusMaster.ModbusMaster master;

//        // Hafıza Değişkenleri
//        ushort eskiInV = 0, eskiInO = 0, eskiInS = 0, eskiBnkrSt = 0;
//        ushort bnkrSt = 0;

//        // Kontrol Bayrakları
//        bool isHaberlesmeActive = false;
//        bool isWriting = false;

//        // TextBox Değişiklik Takibi
//        bool is30Changed = false, is33Changed = false, is34Changed = false;

//        // Komut Hafızaları
//        ushort komutVerisi31 = 0;
//        ushort gonderilecekKomut32 = 0;

//        // Hata takip sayacı
//        int consecutiveErrorCount = 0;

//        // Database Nesnesi
//        DatabaseManager db = new DatabaseManager();

//        //: OTURUM KİMLİĞİ DEĞİŞKENİ BURADA
//        public string aktifOturum = "";

//        public DozajDashboard()
//        {
//            InitializeComponent();
//            ArayuzuSifirla();

//            Timer timerDbSave = new Timer();
//            timerDbSave.Interval = 60000; // 1 Dakika
//            timerDbSave.Tick += (s, e) => PeriyodikVeriKaydet();
//            timerDbSave.Start();
//        }

//        private async Task HaberlesmeDongusu()
//        {
//            while (isHaberlesmeActive)
//            {
//                if (master != null && master.IsOpen() && !isWriting)
//                {
//                    try
//                    {
//                        byte slaveID = (byte)numSlaveId.Value;
//                        byte[] data = await master.ReadHoldingRegistersAsync(slaveID, 0, 35);

//                        if (data != null && data.Length >= 73)
//                        {
//                            consecutiveErrorCount = 0;

//                            this.Invoke((MethodInvoker)delegate
//                            {
//                                VerileriEkranaDagit(data);
//                                if (lblCanlılık.ForeColor == Color.Black) lblCanlılık.ForeColor = Color.Green;
//                                else lblCanlılık.ForeColor = Color.Black;
//                            });
//                        }
//                        else { consecutiveErrorCount++; }
//                    }
//                    catch
//                    {
//                        consecutiveErrorCount++;
//                    }

//                    if (consecutiveErrorCount >= 100)
//                    {
//                        this.Invoke((MethodInvoker)delegate {
//                            if (consecutiveErrorCount == 100)
//                            {
//                                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] !!! BAĞLANTI KOPTU !!!");

//                                //  KOPMA DURUMUNDA AKTİF OTURUM KİMLİĞİYLE KAYIT ATILIR
//                                db.OlayKaydet("Haberleşme zaman aşımı: 100 ardışık hata oluştu.", "ALARM", aktifOturum);

//                                ArayuzuSifirla();
//                            }
//                        });
//                    }
//                }
//                await Task.Delay(150);
//            }
//        }

//        private void PeriyodikVeriKaydet()
//        {
//            if (!isHaberlesmeActive) return;

//            try
//            {
//                double debi = 0, agirlik = 0, hiz = 0, t1 = 0, t2 = 0, bunker = 0;
//                double.TryParse(lblFlowRate.Text.Replace(".", ","), out debi);
//                double.TryParse(lblAnlikAgirlik.Text.Replace(" kg", "").Replace(".", ","), out agirlik);
//                double.TryParse(lblBeltSpeed.Text.Replace("%", "").Replace(".", ","), out hiz);
//                double.TryParse(lblTotalWeight1.Text.Replace(" kg", "").Replace(".", ","), out t1);
//                double.TryParse(lblTotalWeight2.Text.Replace(" kg", "").Replace(".", ","), out t2);
//                double.TryParse(lblBunkerAgirlik.Text.Replace(".", ","), out bunker);

//                int k31 = komutVerisi31;
//                int k32 = gonderilecekKomut32;

//                //  PERİYODİK VERİ KAYDINDA AKTİF OTURUM GÖNDERİLİR
//                db.VeriKaydet(debi, agirlik, hiz, t1, t2, bunker, k31, k32, eskiInV, eskiInO, eskiInS, aktifOturum);
//            }
//            catch { }
//        }

//        private void VerileriEkranaDagit(byte[] data)
//        {
//            ushort GetReg(int addr) => (ushort)((data[3 + (addr * 2)] << 8) | data[3 + (addr * 2) + 1]);

//            try
//            {
//                // Ana Sayısal Göstergeler
//                lblSetDegeri.Text = GetReg(1).ToString();
//                lblAnlikAgirlik.Text = GetReg(4).ToString() + " kg";
//                lblBeltSpeed.Text = "%" + GetReg(5).ToString();
//                lblControlOut.Text = GetReg(6).ToString();
//                lblAgirlikSet.Text = GetReg(11).ToString() + " kg";
//                lblGecenToplam.Text = GetReg(12).ToString() + " kg";
//                lblOnBesleyiciCikis.Text = GetReg(13).ToString();

//                // Ondalıklı Debi
//                int dp = GetReg(17);
//                double bolen = Math.Pow(10, dp);
//                lblFlowRate.Text = (GetReg(3) / bolen).ToString("F" + dp);
//                lblOrtalamaDebi.Text = (GetReg(2) / bolen).ToString("F" + dp);
//                lblNoktanınYeri.Text = dp.ToString();

//                // 32-Bit Toplayıcılar
//                lblTotalWeight1.Text = (GetReg(7) | (GetReg(8) << 16)).ToString() + " kg";
//                lblTotalWeight2.Text = (GetReg(9) | (GetReg(10) << 16)).ToString() + " kg";

//                // Giriş Lambaları
//                ushort inV = GetReg(14);
//                lblGiris0.BackColor = (inV & 0x0001) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris1.BackColor = (inV & 0x0002) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris2.BackColor = (inV & 0x0004) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris3.BackColor = (inV & 0x0008) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblGiris4.BackColor = (inV & 0x0010) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris5.BackColor = (inV & 0x0020) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblGiris6.BackColor = (inV & 0x0040) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris7.BackColor = (inV & 0x0080) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris8.BackColor = (inV & 0x0100) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris9.BackColor = (inV & 0x0200) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris10.ForeColor = (inV & 0x0400) != 0 ? Color.Silver : Color.Silver;
//                lblGiris11.ForeColor = (inV & 0x0800) != 0 ? Color.Silver : Color.Silver;
//                lblGiris12.BackColor = (inV & 0x1000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris13.BackColor = (inV & 0x2000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblGiris14.BackColor = (inV & 0x4000) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblGiris15.BackColor = (inV & 0x8000) != 0 ? Color.Red : SystemColors.MenuBar;

//                // Çıkış Lambaları
//                ushort inO = GetReg(15);
//                lblCikis0.BackColor = (inO & 0x0001) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis1.BackColor = (inO & 0x0002) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis2.BackColor = (inO & 0x0004) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis3.BackColor = (inO & 0x0008) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis4.BackColor = (inO & 0x0010) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis5.BackColor = (inO & 0x0020) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis6.BackColor = (inO & 0x0040) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblCikis7.BackColor = (inO & 0x0080) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblCikis8.ForeColor = (inO & 0x0100) != 0 ? Color.Silver : Color.Silver;
//                lblCikis9.BackColor = (inO & 0x0200) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis10.BackColor = (inO & 0x0400) != 0 ? Color.Yellow : SystemColors.MenuBar;
//                lblCikis11.BackColor = (inO & 0x0800) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblCikis12.BackColor = (inO & 0x1000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblCikis13.BackColor = (inO & 0x2000) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblCikis14.BackColor = (inO & 0x4000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblCikis15.BackColor = (inO & 0x8000) != 0 ? Color.Green : SystemColors.MenuBar;

//                // Status Lambaları
//                ushort inS = GetReg(16);
//                lblStatus0.BackColor = (inS & 0x0001) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus1.BackColor = (inS & 0x0002) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus2.BackColor = (inS & 0x0004) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus3.BackColor = (inS & 0x0008) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblStatus4.BackColor = (inS & 0x0010) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus5.BackColor = (inS & 0x0020) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblStatus6.BackColor = (inS & 0x0040) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus7.BackColor = (inS & 0x0080) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus8.BackColor = (inS & 0x0100) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus9.BackColor = (inS & 0x0200) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus10.BackColor = (inS & 0x0400) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblStatus11.BackColor = (inS & 0x0800) != 0 ? Color.Red : SystemColors.MenuBar;
//                lblStatus12.BackColor = (inS & 0x1000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus13.BackColor = (inS & 0x2000) != 0 ? Color.Green : SystemColors.MenuBar;
//                lblStatus14.ForeColor = (inS & 0x4000) != 0 ? Color.Silver : Color.Silver;
//                lblStatus15.ForeColor = (inS & 0x8000) != 0 ? Color.Silver : Color.Silver;

//                // Bunker Verileri
//                if (chkBunkerMevcut.Checked)
//                {
//                    lblBunkerAgirlik.Text = GetReg(18).ToString();
//                    lblBunkerAgirlikSet.Text = GetReg(19).ToString();
//                    lblBunkerAgirlikKontrolCikis.Text = GetReg(20).ToString();
//                    lblBunkerBosalanAgirlik.Text = GetReg(22).ToString() + " kg";
//                    lblBandTarttigiAgirlik.Text = GetReg(23).ToString() + " kg";

//                    ushort bSt = GetReg(21);
//                    lblKalibrasyonIsleminde.BackColor = (bSt & 0x0001) != 0 ? Color.Green : SystemColors.MenuBar;
//                    lblKalibrasyonToleransIcinde.BackColor = (bSt & 0x0002) != 0 ? Color.Green : SystemColors.MenuBar;
//                    lblKalibrasyonToleransDisinda.BackColor = (bSt & 0x0004) != 0 ? Color.Green : SystemColors.MenuBar;
//                    lblBunkerAgırlikAriza.BackColor = (bSt & 0x0008) != 0 ? Color.Red : SystemColors.MenuBar;
//                    lblbunkerSeviyesiAltSeviyeAltinda.BackColor = (bSt & 0x0010) != 0 ? Color.Green : SystemColors.MenuBar;
//                    lblBunkerSeviyeUstSeviyeUstunde.BackColor = (bSt & 0x0020) != 0 ? Color.Green : SystemColors.MenuBar;
//                    lblKullanılmıyor1.ForeColor = (inV & 0x0400) != 0 ? Color.Silver : Color.Silver;
//                    lblKullanılmıyo2.ForeColor = (inV & 0x0800) != 0 ? Color.Silver : Color.Silver;
//                }

//                // Textbox Güncellemeleri
//                if (!isWriting)
//                {
//                    if (!is30Changed && !txtTargetDose.Focused) txtTargetDose.Text = GetReg(30).ToString();
//                    if (!is33Changed && !txtAgirlikSet.Focused) txtAgirlikSet.Text = GetReg(33).ToString();

//                    if (chkBunkerMevcut.Checked && !is34Changed && !txtBnkrAgirlikSetDeger.Focused)
//                    {
//                        txtBnkrAgirlikSetDeger.Text = GetReg(34).ToString();
//                    }

//                    komutVerisi31 = GetReg(31);
//                    gonderilecekKomut32 = GetReg(32);
//                }

//                // Buton Renkleri
//                btnCalis.BackColor = (komutVerisi31 & 0x0001) != 0 ? Color.Green : Color.Gray;
//                btnOnSistemCalis.BackColor = (komutVerisi31 & 0x0002) != 0 ? Color.Green : Color.Gray;
//                btnMotorCalis.BackColor = (komutVerisi31 & 0x0004) != 0 ? Color.Green : Color.Gray;
//                btnMotorAriza.BackColor = (komutVerisi31 & 0x0008) != 0 ? Color.Red : Color.Gray;
//                btnOnBesleyiciCalis.BackColor = (komutVerisi31 & 0x0010) != 0 ? Color.Green : Color.Gray;
//                btnOnBesleyiciAriza.BackColor = (komutVerisi31 & 0x0020) != 0 ? Color.Red : Color.Gray;
//                btnUzakCalisma.BackColor = (komutVerisi31 & 0x0040) != 0 ? Color.Green : Color.Gray;
//                btnYakinCalisma.BackColor = (komutVerisi31 & 0x0080) != 0 ? Color.Green : Color.Gray;

//                btnTotalSifirla.BackColor = (gonderilecekKomut32 & 0x0001) != 0 ? Color.Orange : Color.Gray;
//                btnArizaSil.BackColor = (gonderilecekKomut32 & 0x0002) != 0 ? Color.Orange : Color.Gray;
//                btnKullanılmıyor1.ForeColor = (gonderilecekKomut32 & 0x0004) != 0 ? Color.Silver : Color.Silver;
//                btnKullanılmıyor2.ForeColor = (gonderilecekKomut32 & 0x0008) != 0 ? Color.Silver : Color.Silver;
//                btnKalibrasyonBasla.BackColor = (gonderilecekKomut32 & 0x0010) != 0 ? Color.Yellow : Color.Gray;
//                btnKalibrasyonKabul.BackColor = (gonderilecekKomut32 & 0x0020) != 0 ? Color.Green : Color.Gray;
//                btnKalibrasyonRed.BackColor = (gonderilecekKomut32 & 0x0040) != 0 ? Color.Red : Color.Gray;
//                btnDolumDurdur.BackColor = (gonderilecekKomut32 & 0x0080) != 0 ? Color.Red : Color.Gray;

//                ArizaKontrolVeKayit(inV, inO, inS, bnkrSt);
//            }
//            catch { }
//        }

//        private async Task GuvenliYaz(ushort adres, ushort deger)
//        {
//            try
//            {
//                isWriting = true;
//                await Task.Delay(50);
//                await master.WriteHoldingRegisterAsync((byte)numSlaveId.Value, adres, deger);
//            }
//            finally
//            {
//                isWriting = false;
//            }
//        }

//        private async void btnConnect_Click(object sender, EventArgs e)
//        {
//            // Bağlantıyı güvenli bir şekilde kapat
//            if (isHaberlesmeActive)
//            {
//                isHaberlesmeActive = false;
//                await Task.Delay(250);
//                if (serial != null) serial.Close();

//                //  MANUEL KESİNTİDE OTURUM KAYDI ATILIR
//                db.OlayKaydet("Kullanıcı bağlantıyı manuel olarak kesti.", "BILGI", aktifOturum);

//                //  OTURUM KİMLİĞİ SIFIRLANIR
//                aktifOturum = "";

//                ArayuzuSifirla();

//                btnConnect.Text = "Bağlan";
//                btnConnect.BackColor = Color.LightGreen;
//                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Bağlantı Kesildi.");
//                return;
//            }

//            try
//            {
//                serial = new SerialPortManager.SerialPortManager(cmbPort.Text, int.Parse(cmbBaudRate.Text), System.IO.Ports.Parity.Even, 8, System.IO.Ports.StopBits.One);
//                master = new ModbusMaster.ModbusMaster(serial);
//                serial.Open();

//                if (serial.IsOpen())
//                {
//                    isHaberlesmeActive = true;
//                    consecutiveErrorCount = 0;

//                    //  YENİ OTURUM KİMLİĞİ OLUŞTURULUR
//                    aktifOturum = "RUN-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

//                    btnConnect.Text = "Bağlantıyı Kes";
//                    btnConnect.BackColor = Color.LightPink;

//                    // BAŞLANGIÇ LOGU AKTİF OTURUM KİMLİĞİYLE ATILIR
//                    db.OlayKaydet($"{cmbPort.Text} portu üzerinden bağlantı sağlandı.", "BILGI", aktifOturum);

//                    _ = HaberlesmeDongusu();
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {cmbPort.Text} Başarıyla Bağlandı.");
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Oturum Başladı: {aktifOturum}");
//                    ButonlariAktiflestir();
//                }
//                else
//                {
//                    throw new Exception("Port fiziksel olarak açılamadı!");
//                }
//            }
//            catch (Exception ex)
//            {
//                isHaberlesmeActive = false;
//                aktifOturum = ""; // Hata varsa oturumu temizle
//                btnConnect.Text = "Bağlan";
//                btnConnect.BackColor = Color.LightGreen;
//                MessageBox.Show($"Bağlantı Hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
//            }
//        }

//        // KOMUT 1 BUTONLARI(Register 31) ---
//        private async void btnCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 1; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnOnSistemCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 2; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnMotorCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 4; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnMotorAriza_Click(object sender, EventArgs e) { komutVerisi31 ^= 8; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnOnBesleyiciCalis_Click(object sender, EventArgs e) { komutVerisi31 ^= 16; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnOnBesleyiciAriza_Click(object sender, EventArgs e) { komutVerisi31 ^= 32; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnUzakCalisma_Click(object sender, EventArgs e) { komutVerisi31 ^= 64; await GuvenliYaz(31, komutVerisi31); }
//        private async void btnYakinCalisma_Click(object sender, EventArgs e) { komutVerisi31 ^= 128; await GuvenliYaz(31, komutVerisi31); }


//        // --- KOMUT 2 BUTONLARI (Register 32) ---
//        private async void btnTotalSifirla_Click(object sender, EventArgs e) 
//        {
//            // 1. İlgili biti SET et (1 yap)
//            gonderilecekKomut32 |= 1;
//            await GuvenliYaz(32, gonderilecekKomut32);

//            //// 2. PLC'nin komutu yakalaması için kısa bir süre (500ms) bekle
//            //await Task.Delay(500);

//            //// 3. İlgili biti RESET et (0 yap) - Hata giderildi: Tam kapsamlı explicit dönüştürme uygulandı.
//            //gonderilecekKomut32 = (ushort)(gonderilecekKomut32 & ~1);
//            //await GuvenliYaz(32, gonderilecekKomut32);
//        }
//        private async void btnArizaSil_Click(object sender, EventArgs e)
//        {
//            // 1. İlgili biti SET et (1 yap)
//            gonderilecekKomut32 |= 2;
//            await GuvenliYaz(32, gonderilecekKomut32);

//            //// 2. PLC'nin komutu yakalaması için bekle
//            //await Task.Delay(500);

//            //// 3. İlgili biti RESET et (0 yap) - Hata giderildi.
//            //gonderilecekKomut32 = (ushort)(gonderilecekKomut32 & ~2);
//            //await GuvenliYaz(32, gonderilecekKomut32);
//        }
//        private async void btnKalibrasyonBasla_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 16; await GuvenliYaz(32, gonderilecekKomut32); }
//        private async void btnKalibrasyonKabul_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 32; await GuvenliYaz(32, gonderilecekKomut32); }
//        private async void btnKalibrasyonRed_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 64; await GuvenliYaz(32, gonderilecekKomut32); }
//        private async void btnDolumDurdur_Click(object sender, EventArgs e) { gonderilecekKomut32 ^= 128; await GuvenliYaz(32, gonderilecekKomut32); }


//        private async void btnAyarlar_Click(object sender, EventArgs e)
//        {
//            if (!isHaberlesmeActive || master == null)
//            {
//                MessageBox.Show("Lütfen önce cihazla bağlantı kurun!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
//                return;
//            }

//            try
//            {
//                if (ushort.TryParse(txtTargetDose.Text, out ushort v30))
//                {
//                    string eskiDeger = lblSetDegeri.Text;
//                    await GuvenliYaz(30, v30);
//                    //  AYARLAR KAYDEDİLİRKEN AKTİF OTURUM GÖNDERİLİR
//                    db.AyarKaydet("Hedef Dozaj", eskiDeger, v30.ToString(), aktifOturum);
//                }

//                if (ushort.TryParse(txtAgirlikSet.Text, out ushort v33))
//                {
//                    string eskiDeger = lblAgirlikSet.Text.Replace(" kg", "");
//                    await GuvenliYaz(33, v33);
//                    db.AyarKaydet("Ağırlık Set", eskiDeger, v33.ToString(), aktifOturum);
//                }

//                if (chkBunkerMevcut.Checked && groupBunker.Enabled)
//                {
//                    if (ushort.TryParse(txtBnkrAgirlikSetDeger.Text, out ushort v34))
//                    {
//                        string eskiDeger = lblBunkerAgirlikSet.Text;
//                        await GuvenliYaz(34, v34);
//                        db.AyarKaydet("Bunker Ağırlık Set", eskiDeger, v34.ToString(), aktifOturum);
//                    }
//                }

//                is30Changed = is33Changed = is34Changed = false;
//                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Ayarlar başarıyla güncellendi.");
//            }
//            catch (Exception ex)
//            {
//                db.OlayKaydet($"Ayar yazma hatası: {ex.Message}", "HATA", aktifOturum);
//            }
//        }

//        private void txtTargetDose_TextChanged(object sender, EventArgs e) { if (txtTargetDose.Focused) is30Changed = true; }
//        private void txtAgirlikSet_TextChanged(object sender, EventArgs e) { if (txtAgirlikSet.Focused) is33Changed = true; }
//        private void txtBnkrAgirlikSetDeger_TextChanged(object sender, EventArgs e) { if (txtBnkrAgirlikSetDeger.Focused) is34Changed = true; }

//        private void chkBunkerMevcut_CheckedChanged(object sender, EventArgs e)
//        {
//            bool isActive = chkBunkerMevcut.Checked;
//            groupBunker.Enabled = isActive;
//            groupBunkerStatus.Enabled = isActive;
//            txtBnkrAgirlikSetDeger.Enabled = isActive;
//            txtBnkrAgirlikSetDeger.BackColor = isActive ? SystemColors.Window : Color.Silver;

//            if (isActive)
//                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm}] Bunker sistemi aktif.");
//            else
//            {
//                lblBunkerAgirlik.Text = "0";
//                lblBunkerAgirlikSet.Text = "0";
//                lblBunkerAgirlikKontrolCikis.Text = "0";
//                lblBunkerBosalanAgirlik.Text = "0";
//                lblBandTarttigiAgirlik.Text = "0";
//                lblKalibrasyonIsleminde.BackColor = SystemColors.MenuBar;
//                lblKalibrasyonToleransIcinde.BackColor = SystemColors.MenuBar;
//                lblKalibrasyonToleransDisinda.BackColor = SystemColors.MenuBar;
//                lblBunkerAgırlikAriza.BackColor = SystemColors.MenuBar;
//                lblbunkerSeviyesiAltSeviyeAltinda.BackColor = SystemColors.MenuBar;
//                lblBunkerSeviyeUstSeviyeUstunde.BackColor = SystemColors.MenuBar;
//                lblKullanılmıyor1.BackColor = SystemColors.MenuBar;
//                lblKullanılmıyo2.BackColor = SystemColors.MenuBar;
//                txtBnkrAgirlikSetDeger.Text = "0";
//                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm}] Bunker sistemi pasif modda.");
//            }
//        }

//        private void btnRaporlar_Click(object sender, EventArgs e)
//        {
//            // Yeni oluşturduğumuz formu, o anki aktif oturum ID'si ile başlatıyoruz
//            RaporForm raporEkrani = new RaporForm(aktifOturum);

//            // ShowDialog ile açarsak, kullanıcı bu pencereyi kapatana kadar 
//            // arkadaki ana forma müdahale edemez. Bu rapor ekranları için en idealidir.
//            raporEkrani.ShowDialog();
//        }

//        private void DozajDashboard_Load(object sender, EventArgs e)
//        {
//            groupBunker.Enabled = false;
//            groupBunkerStatus.Enabled = false;
//            txtBnkrAgirlikSetDeger.Enabled = false;
//        }

//        private void ArayuzuSifirla()
//        {
//            if (this.InvokeRequired)
//            {
//                this.Invoke(new MethodInvoker(ArayuzuSifirla));
//                return;
//            }

//            lblSetDegeri.Text = "0";
//            lblAnlikAgirlik.Text = "0 kg";
//            lblFlowRate.Text = "0.0";
//            lblOrtalamaDebi.Text = "0.0";
//            lblBeltSpeed.Text = "%0";
//            lblControlOut.Text = "0";
//            lblTotalWeight1.Text = "0 kg";
//            lblTotalWeight2.Text = "0 kg";
//            lblGecenToplam.Text = "0 kg";
//            lblAgirlikSet.Text = "0";
//            lblOnBesleyiciCikis.Text = "0";
//            lblNoktanınYeri.Text = "0";

//            txtAgirlikSet.Text = "0";
//            txtBnkrAgirlikSetDeger.Text = "0";
//            txtTargetDose.Text = "0";

//            lblBunkerAgirlik.Text = "0";
//            lblBunkerAgirlikSet.Text = "0";
//            lblBunkerAgirlikKontrolCikis.Text = "0";
//            lblBunkerBosalanAgirlik.Text = "0";
//            lblBandTarttigiAgirlik.Text = "0";

//            btnCalis.Enabled = false;
//            btnOnSistemCalis.Enabled = false;
//            btnMotorCalis.Enabled = false;
//            btnMotorAriza.Enabled = false;
//            btnOnBesleyiciCalis.Enabled = false;
//            btnOnBesleyiciAriza.Enabled = false;
//            btnUzakCalisma.Enabled = false;
//            btnYakinCalisma.Enabled = false;
//            btnTotalSifirla.Enabled = false;
//            btnArizaSil.Enabled = false;
//            btnKullanılmıyor1.Enabled = false;
//            btnKullanılmıyor2.Enabled = false;
//            btnKalibrasyonBasla.Enabled = false;
//            btnKalibrasyonKabul.Enabled = false;
//            btnKalibrasyonRed.Enabled = false;
//            btnDolumDurdur.Enabled = false;

//            btnCalis.BackColor = Color.Gray;
//            btnOnSistemCalis.BackColor = Color.Gray;
//            btnMotorCalis.BackColor = Color.Gray;
//            btnMotorAriza.BackColor = Color.Gray;
//            btnOnBesleyiciCalis.BackColor = Color.Gray;
//            btnOnBesleyiciAriza.BackColor = Color.Gray;
//            btnUzakCalisma.BackColor = Color.Gray;
//            btnYakinCalisma.BackColor = Color.Gray;
//            btnTotalSifirla.BackColor = Color.Gray;
//            btnArizaSil.BackColor = Color.Gray;
//            btnKullanılmıyor1.ForeColor = Color.Silver;
//            btnKullanılmıyor2.ForeColor = Color.Silver;
//            btnKalibrasyonBasla.BackColor = Color.Gray;
//            btnKalibrasyonKabul.BackColor = Color.Gray;
//            btnKalibrasyonRed.BackColor = Color.Gray;
//            btnDolumDurdur.BackColor = Color.Gray;

//            foreach (Control c in this.Controls)
//            {
//                if (c is GroupBox)
//                {
//                    foreach (Control gc in c.Controls)
//                    {
//                        if (gc is Label && (gc.Name.StartsWith("lblGiris") || gc.Name.StartsWith("lblCikis") || gc.Name.StartsWith("lblStatus")))
//                        {
//                            gc.BackColor = SystemColors.MenuBar;
//                        }
//                    }
//                }
//            }

//            lblKalibrasyonIsleminde.BackColor = SystemColors.MenuBar;
//            lblKalibrasyonToleransIcinde.BackColor = SystemColors.MenuBar;
//            lblKalibrasyonToleransDisinda.BackColor = SystemColors.MenuBar;
//            lblBunkerAgırlikAriza.BackColor = SystemColors.MenuBar;
//            lblbunkerSeviyesiAltSeviyeAltinda.BackColor = SystemColors.MenuBar;
//            lblBunkerSeviyeUstSeviyeUstunde.BackColor = SystemColors.MenuBar;
//            lblKullanılmıyor1.BackColor = SystemColors.MenuBar;
//            lblKullanılmıyo2.BackColor = SystemColors.MenuBar;

//            komutVerisi31 = 0;
//            gonderilecekKomut32 = 0;
//            lblSetDegeri.ForeColor = Color.Black;

//            groupBunker.Enabled = false;
//            groupBunkerStatus.Enabled = false;
//            txtBnkrAgirlikSetDeger.Enabled = false;
//            txtBnkrAgirlikSetDeger.BackColor = Color.Silver;
//        }

//        private void ButonlariAktiflestir()
//        {
//            if (this.InvokeRequired)
//            {
//                this.Invoke(new MethodInvoker(ButonlariAktiflestir));
//                return;
//            }

//            btnCalis.Enabled = true;
//            btnOnSistemCalis.Enabled = true;
//            btnMotorCalis.Enabled = true;
//            btnMotorAriza.Enabled = true;
//            btnOnBesleyiciCalis.Enabled = true;
//            btnOnBesleyiciAriza.Enabled = true;
//            btnUzakCalisma.Enabled = true;
//            btnYakinCalisma.Enabled = true;
//            btnTotalSifirla.Enabled = true;
//            btnArizaSil.Enabled = true;
//            btnKullanılmıyor1.Enabled = true;
//            btnKullanılmıyor2.Enabled = true;
//            btnKalibrasyonBasla.Enabled = true;
//            btnKalibrasyonKabul.Enabled = true;
//            btnKalibrasyonRed.Enabled = true;
//            btnDolumDurdur.Enabled = true;

//            btnCalis.BackColor = Color.Gray;
//            btnOnSistemCalis.BackColor = Color.Gray;
//            btnMotorCalis.BackColor = Color.Gray;
//            btnMotorAriza.BackColor = Color.Gray;
//            btnOnBesleyiciCalis.BackColor = Color.Gray;
//            btnOnBesleyiciAriza.BackColor = Color.Gray;
//            btnUzakCalisma.BackColor = Color.Gray;
//            btnYakinCalisma.BackColor = Color.Gray;
//            btnTotalSifirla.BackColor = Color.Gray;
//            btnArizaSil.BackColor = Color.Gray;
//            btnKalibrasyonBasla.BackColor = Color.Gray;
//            btnKalibrasyonKabul.BackColor = Color.Gray;
//            btnKalibrasyonRed.BackColor = Color.Gray;
//            btnDolumDurdur.BackColor = Color.Gray;

//            btnKullanılmıyor1.ForeColor = Color.Black;
//            btnKullanılmıyor2.ForeColor = Color.Black;
//        }

//        private void ArizaKontrolVeKayit(ushort inV, ushort inO, ushort inS, ushort bnkrSt)
//        {
//            CheckAndLog(inS, eskiInS, 0x0010, "Band Kaydı Arıza");
//            CheckAndLog(inS, eskiInS, 0x0020, "Acil Dur Durumu");
//            CheckAndLog(inS, eskiInS, 0x0040, "Ağırlık Arıza");
//            CheckAndLog(inS, eskiInS, 0x0080, "Sıfır Arıza");
//            CheckAndLog(inS, eskiInS, 0x0100, "Hız Arıza");
//            CheckAndLog(inS, eskiInS, 0x0200, "Debi Arıza");
//            CheckAndLog(inS, eskiInS, 0x0400, "Normal Çalışma Engel");
//            CheckAndLog(inS, eskiInS, 0x0800, "Tolerans Arıza");
//            CheckAndLog(inS, eskiInS, 0x2000, "Motor Arıza");

//            if (chkBunkerMevcut.Checked)
//                CheckAndLog(bnkrSt, eskiBnkrSt, 0x0008, "Bunker Ağırlık Arızası");

//            eskiInV = inV; eskiInO = inO; eskiInS = inS; eskiBnkrSt = bnkrSt;
//        }

//        //  YENİ: CheckAndLog METODU DA AKTİF OTURUM KİMLİĞİYLE KAYIT ATAR
//        private void CheckAndLog(ushort current, ushort old, ushort mask, string arizaAdi)
//        {
//            bool suAnHataVar = (current & mask) != 0;
//            bool oncedenHataVarmiydi = (old & mask) != 0;

//            if (suAnHataVar && !oncedenHataVarmiydi)
//            {
//                // HATA BAŞLADI (Update kalmadı, sözlük kullanılmıyor)
//                db.OlayKaydet(arizaAdi + " BAŞLADI", "ALARM", aktifOturum);

//                this.Invoke((MethodInvoker)delegate {
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {arizaAdi} Başladı.");
//                });
//            }
//            else if (!suAnHataVar && oncedenHataVarmiydi)
//            {
//                // HATA GİDERİLDİ (Yeni satır atılır)
//                db.OlayKaydet(arizaAdi + " GİDERİLDİ", "ALARM", aktifOturum);

//                this.Invoke((MethodInvoker)delegate {
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {arizaAdi} Giderildi.");
//                });
//            }
//        }
//    }
//}
