using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using ModbusLibrary;
using ModbusLibrary.SerialPortManager;
using ModbusLibrary.ModbusMaster;
using System.Windows.Controls.Primitives;
using WPF;

namespace AC10
{
    public partial class MainWindow : Window
    {
        // Haberleşme ve Veritabanı Nesneleri
        SerialPortManager serial = null!;
        ModbusMaster master = null!;
        DatabaseManager db = new DatabaseManager();

        // Kontrol Bayrakları ve Değişkenler
        bool isCommunicationActive = false;
        bool isWriting = false;
        int consecutiveErrorCount = 0;

        ushort commandData31 = 0;
        ushort commandToSend32 = 0;

        int currentDecimalPoint = 0; 

        ushort oldInV = 0, oldInO = 0, oldInS = 0, oldBnkrSt = 0;
        public string activeSession = "";

        // UI Renk Paleti
        SolidColorBrush colorOff = new SolidColorBrush(Color.FromRgb(49, 55, 67));
        SolidColorBrush colorSilver = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Kullanılmayanlar
        SolidColorBrush colorRed = new SolidColorBrush(Color.FromRgb(224, 108, 117));
        SolidColorBrush colorGreen = new SolidColorBrush(Color.FromRgb(152, 195, 121));
        SolidColorBrush colorYellow = new SolidColorBrush(Color.FromRgb(229, 192, 123));

        // Dinamik LED Dizileri
        Border[] ledsInput = new Border[16];
        Border[] ledsOutput = new Border[16];
        Border[] ledsStatus = new Border[16];
        Border[] ledsBunker = new Border[8];

        // Değişiklik Takip Bayrakları
        bool is30Changed = false, is33Changed = false, is34Changed = false;

        public MainWindow()
        {
            InitializeComponent();
            CreateIndicatorLamps();
            ResetUI();

            // Kullanıcı yazı yazmaya başladığında kutuları kilitleyen olaylar
            txtTargetDose.TextChanged += (s, e) => { if (txtTargetDose.IsFocused) is30Changed = true; };
            txtWeightSet.TextChanged += (s, e) => { if (txtWeightSet.IsFocused) is33Changed = true; };
            txtBunkerWeightSet.TextChanged += (s, e) => { if (txtBunkerWeightSet.IsFocused) is34Changed = true; };

            if (chkBunkerEnabled != null)
            {
                chkBunkerEnabled.IsChecked = false;
                chkBunkerEnabled_CheckedChanged(null, null);
            }

            // Veritabanı Periyodik Kayıt Zamanlayıcısı
            DispatcherTimer timerDbSave = new DispatcherTimer();
            timerDbSave.Interval = TimeSpan.FromMinutes(1);
            timerDbSave.Tick += (s, e) => SavePeriodicData();
            timerDbSave.Start();
        }

        // --- DİNAMİK I/O PANELİ OLUŞTURMA ---
        private void CreateIndicatorLamps()
        {
            string[] inputNames = { "Çalış", "Ön Sistem Çalışıyor", "Motor Çalışıyor", "Motor Arıza", "Ön Besleyici Çalışıyor", "Ön Besleyici Arızalı", "Uzak Çalışma", "Yakın Çalışma", "Toplayıcı Sıfırla", "Arıza Sil", "Kullanılmıyor", "Kullanılmıyor", "Kalibrasyon Başla", "Kalibrasyon Kabul", "Kalıbrasyon Red", "Kalibrasyon Durduruldu" };
            string[] outputNames = { "Tolerans Arıza", "Ağırlık Arıza", "Acil Dur", "Band Kaydı Arızası", "Hız Arıza", "Debi Arıza", "Ön Besleyici Çalış", "Çalış", "Kullanılmıyor", "Patinaj Arıza", "Kalibrasyon Red/Kabul Bekleme", "Kalibrasyon Tolerans içinde", "Kalibrasyon Başladı", "Motor Arıza", "Sıfır Arıza", "Remote" };
            string[] statusNames = { "Sistem Çalışıyor", "Yakın Çalışma Durumu", "Manuel Durumda", "Kalibrasyon Durumunda", "Band Kaydı Arızası", "Acil Dur Durumu", "Ağırlık Arıza", "Sıfır Arıza", "Hız Arıza", "Debi Arıza", "Normal Çalışma İçin Bir Engel Var", "Tolerans Arıza", "Sayıcı Darbesi", "Motor Arıza", "Kullanılmıyor", "Kullanılmıyor" };
            string[] bunkerNames = { "Kalibrasyon İşleminde", "Kalibrasyon Tolerans İçinde", "Kalibrasyon Tolerans Dışında Ret/Kabul Bekle.", "Bunker Ağırlık Arıza", "Bunker Seviyesi Alt Seviye Altında", "Bunker Seviyesi Üst Seviyenin Üzerinde", "Kullanılmıyor", "Kullanılmıyor" };

            for (int i = 0; i < 16; i++) { ledsInput[i] = CreateLed(gridInputs, inputNames[i]); }
            for (int i = 0; i < 16; i++) { ledsOutput[i] = CreateLed(gridOutputs, outputNames[i]); }
            for (int i = 0; i < 16; i++) { ledsStatus[i] = CreateLed(gridStatus, statusNames[i]); }
            for (int i = 0; i < 8; i++) { ledsBunker[i] = CreateLed(gridBunkerStatus, bunkerNames[i]); }
        }

        private Border CreateLed(UniformGrid parent, string text)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            Border led = new Border
            {
                Width = 24,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = colorOff,
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 27, 33)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            if (text == "Kullanılmıyor" || text == "---")
            {
                tb.Foreground = colorSilver;
                tb.FontWeight = FontWeights.Normal;
            }

            sp.Children.Add(led);
            sp.Children.Add(tb);
            parent.Children.Add(sp);
            return led;
        }

        private double ParseDoubleSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0;
           
            string cleaned = val.Replace(" ton/sa", "").Replace(" ton", "").Replace(" kg", "").Replace("%", "").Replace(",", ".");
            double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }

        // --- BAĞLANTI YÖNETİMİ ---
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (isCommunicationActive)
            {
                isCommunicationActive = false;
                await Task.Delay(250);
                if (serial != null) serial.Close();

                db.OlayKaydet("Kullanıcı bağlantıyı manuel olarak kesti.", "BILGI", activeSession);
                activeSession = "";

                btnConnect.Content = "Sisteme Bağlan";
                btnConnect.Background = colorGreen;
                btnConnect.Foreground = new SolidColorBrush(Color.FromRgb(24, 27, 33));
                lblHeartbeat.Fill = colorRed;

                ResetUI();
                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Bağlantı Kesildi.");
                return;
            }

            try
            {
                int baud = int.Parse(txtBaudRate.Text);
                serial = new SerialPortManager(txtPort.Text, baud, System.IO.Ports.Parity.Even, 8, System.IO.Ports.StopBits.One);
                master = new ModbusMaster(serial);
                serial.Open();

                if (serial.IsOpen())
                {
                    isCommunicationActive = true;
                    consecutiveErrorCount = 0;
                    activeSession = "RUN-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

                    btnConnect.Content = "Bağlantıyı Kes";
                    btnConnect.Background = colorRed;
                    btnConnect.Foreground = Brushes.White;

                    db.OlayKaydet($"{txtPort.Text} üzerinden bağlantı sağlandı.", "BILGI", activeSession);
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {txtPort.Text} Bağlandı. Oturum: {activeSession}");

                    _ = CommunicationLoop();
                }
            }
            catch (Exception ex)
            {
                isCommunicationActive = false; activeSession = "";
                MessageBox.Show($"Bağlantı Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CommunicationLoop()
        {
            while (isCommunicationActive)
            {
                if (master != null && master.IsOpen() && !isWriting)
                {
                    try
                    {
                        byte slaveID = byte.Parse(txtSlaveId.Text);
                        byte[] data = await master.ReadHoldingRegistersAsync(slaveID, 0, 35);

                        if (data != null && data.Length >= 73)
                        {
                            consecutiveErrorCount = 0;
                            Dispatcher.Invoke(() => {
                                UpdateUIWithData(data);
                                lblHeartbeat.Fill = (lblHeartbeat.Fill == colorGreen) ? colorOff : colorGreen;
                            });
                        }
                        else { consecutiveErrorCount++; }
                    }
                    catch { consecutiveErrorCount++; }

                    if (consecutiveErrorCount >= 100)
                    {
                        Dispatcher.Invoke(() => {
                            if (consecutiveErrorCount == 100)
                            {
                                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] !!! BAĞLANTI KOPTU !!!");
                                db.OlayKaydet("Haberleşme zaman aşımı: 100 ardışık hata oluştu.", "ALARM", activeSession);
                                lblHeartbeat.Fill = colorRed;
                                ResetUI();
                            }
                        });
                    }
                }
                await Task.Delay(150);
            }
        }

        // --- VERİLERİ EKRANA DAĞITMA ---
        private void UpdateUIWithData(byte[] data)
        {
            ushort GetReg(int addr) => (ushort)((data[3 + (addr * 2)] << 8) | data[3 + (addr * 2) + 1]);

            try
            {
                
                currentDecimalPoint = GetReg(17);
                lblDecimalPoint.Text = currentDecimalPoint.ToString();
                double bolen = Math.Pow(10, currentDecimalPoint);

                // 1. ANA SAYISAL GÖSTERGELER (Reg 1 Bölünerek Yazılır)
                lblSetPoint.Text = (GetReg(1) / bolen).ToString("F" + currentDecimalPoint) + " ton/sa";
                lblCurrentWeight.Text = GetReg(4).ToString() + " kg";
                lblBeltSpeed.Text = GetReg(5).ToString();
                lblControlOutput.Text = GetReg(6).ToString();
                lblWeightSetRead.Text = GetReg(11).ToString() + " ton";
                lblTotalPassed.Text = GetReg(12).ToString() + " ton";
                lblPreFeederOutput.Text = GetReg(13).ToString();

                // 2. ONDALIKLI DEBİ HESAPLARI (Reg 2 ve 3 Bölünerek Yazılır)
                lblFlowRate.Text = (GetReg(3) / bolen).ToString("F" + currentDecimalPoint) + " ton/sa";
                lblAverageFlow.Text = (GetReg(2) / bolen).ToString("F" + currentDecimalPoint) + " ton/sa";

                // 3. 32-BIT TOPLAYICILAR
                lblTotalWeight1.Text = ((GetReg(7) << 16) | GetReg(8)).ToString() + " ton";
                lblTotalWeight2.Text = ((GetReg(9) << 16) | GetReg(10)).ToString() + " ton";

                // 4. I/O LAMBALARI
                ushort inV = GetReg(14);
                ushort inO = GetReg(15);
                ushort inS = GetReg(16);
                ushort bnkrSt = chkBunkerEnabled.IsChecked == true ? GetReg(21) : (ushort)0;

                for (int i = 0; i < 16; i++)
                {
                    bool isInput = (inV & (1 << i)) != 0;
                    Brush vColor = colorGreen;
                    if (i == 3 || i == 5 || i == 14 || i == 15) vColor = colorRed;
                    if (i == 10 || i == 11) vColor = colorSilver;
                    ledsInput[i].Background = isInput && (i != 10 && i != 11) ? vColor : colorOff;

                    bool isOutput = (inO & (1 << i)) != 0;
                    Brush oColor = colorGreen;
                    if ((i >= 0 && i <= 5) || i == 9 || i == 13 || i==14) oColor = colorRed;
                    if (i == 10) oColor = colorYellow;
                    if (i == 8) oColor = colorSilver;
                    ledsOutput[i].Background = isOutput && (i != 8) ? oColor : colorOff;

                    bool isStatus = (inS & (1 << i)) != 0;
                    Brush sColor = colorGreen;
                    if (i >= 4 && i <= 11 || i == 13) sColor = colorRed;
                    if (i >= 14) sColor = colorSilver;
                    ledsStatus[i].Background = isStatus && (i < 14) ? sColor : colorOff;
                }

                // 5. BUNKER SİSTEMİ
                if (chkBunkerEnabled.IsChecked == true)
                {
                    lblBunkerWeight.Text = GetReg(18).ToString() + " ton";
                    lblBunkerWeightSetRead.Text = GetReg(19).ToString() + " ton";
                    lblBunkerControlOutput.Text = GetReg(20).ToString();
                    lblBunkerDischargedWeight.Text = GetReg(22).ToString() + " kg";
                    lblBeltWeighedWeight.Text = GetReg(23).ToString() + " kg";

                    for (int i = 0; i < 8; i++)
                    {
                        bool isBunker = (bnkrSt & (1 << i)) != 0;
                        Brush bColor = colorGreen;
                        if (i == 3) bColor = colorRed;
                        if (i == 2) bColor = colorYellow;
                        if (i >= 6) bColor = colorSilver;
                        ledsBunker[i].Background = isBunker && (i < 6) ? bColor : colorOff;
                    }
                }

                // 6. TEXTBOX VE BUTON SENKRONİZASYONU
                if (!isWriting)
                {
                    // Reg 30 (Hedef Dozaj) okunurken de bölen ile bölünerek ekrana yansıtılır (Örn: 125 gelirse 12.5 yazar)
                    if (!is30Changed && !txtTargetDose.IsFocused)
                        txtTargetDose.Text = (GetReg(30) / bolen).ToString("F" + currentDecimalPoint);

                    if (!is33Changed && !txtWeightSet.IsFocused) txtWeightSet.Text = GetReg(33).ToString();
                    if (chkBunkerEnabled.IsChecked == true && !is34Changed && !txtBunkerWeightSet.IsFocused)
                        txtBunkerWeightSet.Text = GetReg(34).ToString();

                    commandData31 = GetReg(31);
                    commandToSend32 = GetReg(32);
                }

                btnSystemRun.Background = (commandData31 & 0x0001) != 0 ? colorGreen : colorOff;
                btnMotorRun.Background = (commandData31 & 0x0004) != 0 ? colorGreen : colorOff;
                btnPreSystemRun.Background = (commandData31 & 0x0002) != 0 ? colorGreen : colorOff;
                btnMotorFault.Background = (commandData31 & 0x0008) != 0 ? colorRed : colorOff;
                btnPreFeederRun.Background = (commandData31 & 0x0010) != 0 ? colorGreen : colorOff;
                btnPreFeederFault.Background = (commandData31 & 0x0020) != 0 ? colorRed : colorOff;
                btnRemoteOperation.Background = (commandData31 & 0x0040) != 0 ? colorGreen : colorOff;
                btnLocalOperation.Background = (commandData31 & 0x0080) != 0 ? colorGreen : colorOff;

                btnResetTotalizer.Background = (commandToSend32 & 0x0001) != 0 ? colorYellow : colorOff;
                btnResetFaults.Background = (commandToSend32 & 0x0002) != 0 ? colorYellow : colorOff;
                btnStartCalibration.Background = (commandToSend32 & 0x0010) != 0 ? colorYellow : colorOff;
                btnAcceptCalibration.Background = (commandToSend32 & 0x0020) != 0 ? colorGreen : colorOff;
                btnRejectCalibration.Background = (commandToSend32 & 0x0040) != 0 ? colorRed : colorOff;
                btnStopFilling.Background = (commandToSend32 & 0x0080) != 0 ? colorRed : colorOff;

                CheckAndLogAlarms(inV, inO, inS, bnkrSt);
            }
            catch { }
        }

        // --- ALARM DENETİM VE DB KAYIT ---
        private void CheckAndLogAlarms(ushort inV, ushort inO, ushort inS, ushort bnkrSt)
        {
            CheckAndLog(inS, oldInS, 0x0010, "Band Kaydı Arıza");
            CheckAndLog(inS, oldInS, 0x0020, "Acil Dur Durumu");
            CheckAndLog(inS, oldInS, 0x0040, "Ağırlık Arıza");
            CheckAndLog(inS, oldInS, 0x0080, "Sıfır Arıza");
            CheckAndLog(inS, oldInS, 0x0100, "Hız Arıza");
            CheckAndLog(inS, oldInS, 0x0200, "Debi Arıza");
            CheckAndLog(inS, oldInS, 0x0400, "Normal Çalışma Engel");
            CheckAndLog(inS, oldInS, 0x0800, "Tolerans Arıza");
            CheckAndLog(inS, oldInS, 0x2000, "Motor Arıza");

            if (chkBunkerEnabled.IsChecked == true)
                CheckAndLog(bnkrSt, oldBnkrSt, 0x0008, "Bunker Ağırlık Arızası");

            oldInV = inV; oldInO = inO; oldInS = inS; oldBnkrSt = bnkrSt;
        }

        private void CheckAndLog(ushort current, ushort old, ushort mask, string alarmName)
        {
            bool hasErrorNow = (current & mask) != 0;
            bool hadErrorBefore = (old & mask) != 0;

            if (hasErrorNow && !hadErrorBefore)
            {
                db.OlayKaydet(alarmName + " BAŞLADI", "ALARM", activeSession);
                Dispatcher.Invoke(() => lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {alarmName} Başladı."));
            }
            else if (!hasErrorNow && hadErrorBefore)
            {
                db.OlayKaydet(alarmName + " GİDERİLDİ", "ALARM", activeSession);
                Dispatcher.Invoke(() => lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {alarmName} Giderildi."));
            }
        }

        private void SavePeriodicData()
        {
            if (!isCommunicationActive) return;
            try
            {
                double flowRate = ParseDoubleSafe(lblFlowRate.Text);
                double currentWeight = ParseDoubleSafe(lblCurrentWeight.Text);
                double beltSpeed = ParseDoubleSafe(lblBeltSpeed.Text);
                double total1 = ParseDoubleSafe(lblTotalWeight1.Text);
                double total2 = ParseDoubleSafe(lblTotalWeight2.Text);
                double bunkerWeight = ParseDoubleSafe(lblBunkerWeight.Text);

                db.VeriKaydet(flowRate, currentWeight, beltSpeed, total1, total2, bunkerWeight, commandData31, commandToSend32, oldInV, oldInO, oldInS, activeSession);
            }
            catch { }
        }

        // --- GÜVENLİ YAZMA VE BUTON OLAYLARI ---
        private async Task SafeWriteAsync(ushort address, ushort value)
        {
            if (master == null || !isCommunicationActive) return;
            try
            {
                isWriting = true;
                await Task.Delay(50);
                byte slaveID = byte.Parse(txtSlaveId.Text);
                await master.WriteHoldingRegisterAsync(slaveID, address, value);
                await Task.Delay(100);
            }
            finally { isWriting = false; }
        }

        // Komut 1 
        private async void btnSystemRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 1; await SafeWriteAsync(31, commandData31); }
        private async void btnPreSystemRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 2; await SafeWriteAsync(31, commandData31); }
        private async void btnMotorRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 4; await SafeWriteAsync(31, commandData31); }
        private async void btnMotorFault_Click(object sender, RoutedEventArgs e) { commandData31 ^= 8; await SafeWriteAsync(31, commandData31); }
        private async void btnPreFeederRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 16; await SafeWriteAsync(31, commandData31); }
        private async void btnPreFeederFault_Click(object sender, RoutedEventArgs e) { commandData31 ^= 32; await SafeWriteAsync(31, commandData31); }
        private async void btnRemoteOperation_Click(object sender, RoutedEventArgs e) { commandData31 ^= 64; await SafeWriteAsync(31, commandData31); }
        private async void btnLocalOperation_Click(object sender, RoutedEventArgs e) { commandData31 ^= 128; await SafeWriteAsync(31, commandData31); }

        // Komut 2 (Pulse Mantığı)
        private async void btnResetTotalizer_Click(object sender, RoutedEventArgs e)
        {
            commandToSend32 |= 1;
            await SafeWriteAsync(32, commandToSend32);
            await Task.Delay(500);
            commandToSend32 = (ushort)(commandToSend32 & ~1);
            await SafeWriteAsync(32, commandToSend32);
        }

        private async void btnResetFaults_Click(object sender, RoutedEventArgs e)
        {
            commandToSend32 |= 2;
            await SafeWriteAsync(32, commandToSend32);
            await Task.Delay(500);
            commandToSend32 = (ushort)(commandToSend32 & ~2);
            await SafeWriteAsync(32, commandToSend32);
        }

        private async void btnStartCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 16; await SafeWriteAsync(32, commandToSend32); }
        private async void btnAcceptCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 32; await SafeWriteAsync(32, commandToSend32); }
        private async void btnRejectCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 64; await SafeWriteAsync(32, commandToSend32); }
        private async void btnStopFilling_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 128; await SafeWriteAsync(32, commandToSend32); }

        //Sadece kullanıcının değiştirdiği değerleri gönderen akıllı Ayar Gönderme Metodu
        private async void btnSendSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!isCommunicationActive) { MessageBox.Show("Önce cihaza bağlanın!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            try
            {
                bool ayarGonderildi = false;

                // Sadece kullanıcı dokunduysa ve değiştirdiyse gönder
                if (is30Changed)
                {
                    // Operatör 12.5 yazdığında bunu Noktanın Yeri (Örn: 10) ile çarpıp 125 tamsayısına dönüştürürüz
                    double girilenDeger = ParseDoubleSafe(txtTargetDose.Text);
                    ushort v30 = (ushort)(Math.Round(girilenDeger * Math.Pow(10, currentDecimalPoint)));

                    db.AyarKaydet("Hedef Dozaj", "0", girilenDeger.ToString(), activeSession);
                    await SafeWriteAsync(30, v30);
                    ayarGonderildi = true;
                }

                if (is33Changed && ushort.TryParse(txtWeightSet.Text, out ushort v33))
                {
                    db.AyarKaydet("Ağırlık Set", "0", v33.ToString(), activeSession);
                    await SafeWriteAsync(33, v33);
                    ayarGonderildi = true;
                }

                if (chkBunkerEnabled.IsChecked == true && is34Changed && ushort.TryParse(txtBunkerWeightSet.Text, out ushort v34))
                {
                    db.AyarKaydet("Bunker Ağırlık Set", "0", v34.ToString(), activeSession);
                    await SafeWriteAsync(34, v34);
                    ayarGonderildi = true;
                }

                await Task.Delay(600);

                is30Changed = false;
                is33Changed = false;
                is34Changed = false;

                if (ayarGonderildi)
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Değiştirilen ayarlar cihaza yazıldı.");
                else
                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Değiştirilen yeni bir ayar bulunamadı.");
            }
            catch { }
        }

        //  PASİF OLUNCA HER ŞEYİ (YAZILAR DAHİL) SOLUKLAŞTIR
        private void chkBunkerEnabled_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (pnlBunker != null)
            {
                bool isActive = chkBunkerEnabled.IsChecked == true;
                pnlBunker.IsEnabled = isActive;

                if (txtBunkerWeightSet != null)
                {
                    txtBunkerWeightSet.IsEnabled = isActive;
                    txtBunkerWeightSet.Background = isActive ? new SolidColorBrush(Color.FromRgb(33, 37, 45)) : new SolidColorBrush(Color.FromRgb(49, 55, 67));
                    txtBunkerWeightSet.Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(92, 99, 112));

                    if (!isActive) txtBunkerWeightSet.Text = "0";
                }

                SolidColorBrush fadedText = new SolidColorBrush(Color.FromRgb(92, 99, 112));
                SolidColorBrush activeText = new SolidColorBrush(Color.FromRgb(171, 178, 191));

                if (isActive)
                {
                    lblBunkerWeight.Foreground = new SolidColorBrush(Color.FromRgb(229, 192, 123));
                    lblBunkerWeightSetRead.Foreground = new SolidColorBrush(Color.FromRgb(229, 192, 123));
                    lblBunkerControlOutput.Foreground = new SolidColorBrush(Color.FromRgb(209, 154, 102));
                    lblBunkerDischargedWeight.Foreground = new SolidColorBrush(Color.FromRgb(198, 120, 221));
                    lblBeltWeighedWeight.Foreground = new SolidColorBrush(Color.FromRgb(86, 182, 194));
                }
                else
                {
                    lblBunkerWeight.Text = "0 ton";
                    lblBunkerWeightSetRead.Text = "0 ton";
                    lblBunkerControlOutput.Text = "0";
                    lblBunkerDischargedWeight.Text = "0 kg";
                    lblBeltWeighedWeight.Text = "0 kg";

                    lblBunkerWeight.Foreground = fadedText;
                    lblBunkerWeightSetRead.Foreground = fadedText;
                    lblBunkerControlOutput.Foreground = fadedText;
                    lblBunkerDischargedWeight.Foreground = fadedText;
                    lblBeltWeighedWeight.Foreground = fadedText;

                    if (ledsBunker != null)
                    {
                        foreach (var led in ledsBunker)
                        {
                            if (led != null) led.Background = colorOff;
                        }
                    }
                }

                if (gridBunkerStatus != null)
                {
                    foreach (UIElement item in gridBunkerStatus.Children)
                    {
                        if (item is StackPanel sp)
                        {
                            foreach (UIElement child in sp.Children)
                            {
                                if (child is TextBlock tb && tb.Text != "Kullanılmıyor" && tb.Text != "---")
                                {
                                    tb.Foreground = isActive ? activeText : fadedText;
                                }
                            }
                        }
                    }
                }

                lstLogs.Items.Insert(0, isActive ? $"[{DateTime.Now:HH:mm:ss}] Bunker sistemi aktif." : $"[{DateTime.Now:HH:mm:ss}] Bunker sistemi pasif modda.");
            }
        }

        private void btnReports_Click(object sender, RoutedEventArgs e)
        {
            RaporWindow raporEkrani = new RaporWindow(activeSession);
            raporEkrani.ShowDialog();
        }

        private void ResetUI()
        {
            lblSetPoint.Text = "0 ton/sa"; lblFlowRate.Text = "0 ton/sa"; lblCurrentWeight.Text = "0 kg";
            lblAverageFlow.Text = "0 ton/sa"; lblBeltSpeed.Text = "0"; lblTotalPassed.Text = "0 ton";
            lblTotalWeight1.Text = "0 ton"; lblTotalWeight2.Text = "0 ton"; lblWeightSetRead.Text = "0 ton";
            lblBunkerWeight.Text = "0 ton"; lblPreFeederOutput.Text = "0"; lblDecimalPoint.Text = "0";
            lblControlOutput.Text = "0"; lblBunkerControlOutput.Text = "0"; lblBunkerWeightSetRead.Text = "0 ton";
            lblBunkerDischargedWeight.Text = "0 kg"; lblBeltWeighedWeight.Text = "0 kg";

            foreach (var b in ledsInput) b.Background = colorOff;
            foreach (var b in ledsOutput) b.Background = colorOff;
            foreach (var b in ledsStatus) b.Background = colorOff;
            foreach (var b in ledsBunker) b.Background = colorOff;

            commandData31 = 0; commandToSend32 = 0;
            btnSystemRun.Background = colorOff; btnMotorRun.Background = colorOff;
            btnPreSystemRun.Background = colorOff; btnMotorFault.Background = colorOff;
            btnPreFeederRun.Background = colorOff; btnPreFeederFault.Background = colorOff;
            btnRemoteOperation.Background = colorOff; btnLocalOperation.Background = colorOff;
            btnResetTotalizer.Background = colorOff; btnResetFaults.Background = colorOff;
            btnStartCalibration.Background = colorOff; btnAcceptCalibration.Background = colorOff;
            btnRejectCalibration.Background = colorOff; btnStopFilling.Background = colorOff;
        }
    }
}

























//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Media;
//using System.Windows.Shapes;
//using System.Windows.Threading;

//using ModbusLibrary;
//using ModbusLibrary.SerialPortManager;
//using ModbusLibrary.ModbusMaster;
//using System.Windows.Controls.Primitives;
//using WPF;
//namespace AC10
//{
//    public partial class MainWindow : Window
//    {
//        // Haberleşme ve Veritabanı Nesneleri
//        SerialPortManager serial = null!;
//        ModbusMaster master = null!;
//        DatabaseManager db = new DatabaseManager();

//        // Kontrol Bayrakları ve Değişkenler
//        bool isCommunicationActive = false;
//        bool isWriting = false;
//        int consecutiveErrorCount = 0;

//        ushort commandData31 = 0;
//        ushort commandToSend32 = 0;

//        ushort oldInV = 0, oldInO = 0, oldInS = 0, oldBnkrSt = 0;
//        public string activeSession = "";

//        // UI Renk Paleti
//        SolidColorBrush colorOff = new SolidColorBrush(Color.FromRgb(49, 55, 67));
//        SolidColorBrush colorSilver = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Kullanılmayanlar
//        SolidColorBrush colorRed = new SolidColorBrush(Color.FromRgb(224, 108, 117));
//        SolidColorBrush colorGreen = new SolidColorBrush(Color.FromRgb(152, 195, 121));
//        SolidColorBrush colorYellow = new SolidColorBrush(Color.FromRgb(229, 192, 123));

//        // Dinamik LED Dizileri (Modern Dikdörtgen Border Yapısı)
//        Border[] ledsInput = new Border[16];
//        Border[] ledsOutput = new Border[16];
//        Border[] ledsStatus = new Border[16];
//        Border[] ledsBunker = new Border[8];

//        // Değişiklik Takip Bayrakları
//        bool is30Changed = false, is33Changed = false, is34Changed = false;

//        public MainWindow()
//        {
//            InitializeComponent();
//            CreateIndicatorLamps();
//            ResetUI();

//            // 🔥 YENİ: Kullanıcı yazı yazmaya başladığında kutuları kilitleyen olaylar (Eski verinin ezilmesini önler)
//            txtTargetDose.TextChanged += (s, e) => { if (txtTargetDose.IsFocused) is30Changed = true; };
//            txtWeightSet.TextChanged += (s, e) => { if (txtWeightSet.IsFocused) is33Changed = true; };
//            txtBunkerWeightSet.TextChanged += (s, e) => { if (txtBunkerWeightSet.IsFocused) is34Changed = true; };

//            // 🔥 UYGULAMA İLK AÇILDIĞINDA BUNKER PANELİNİ PASİF VE SOLUK HALE GETİR
//            if (chkBunkerEnabled != null)
//            {
//                chkBunkerEnabled.IsChecked = false;
//                chkBunkerEnabled_CheckedChanged(null, null);
//            }

//            // Veritabanı Periyodik Kayıt Zamanlayıcısı (1 Dakika)
//            DispatcherTimer timerDbSave = new DispatcherTimer();
//            timerDbSave.Interval = TimeSpan.FromMinutes(1);
//            timerDbSave.Tick += (s, e) => SavePeriodicData();
//            timerDbSave.Start();
//        }

//        // --- DİNAMİK I/O PANELİ OLUŞTURMA ---
//        private void CreateIndicatorLamps()
//        {
//            string[] inputNames = { "Çalış", "Ön Sistem Çalışıyor", "Motor Çalışıyor", "Motor Arıza", "Ön Besleyici Çalışıyor", "Ön Besleyici Arızalı", "Uzak Çalışma", "Yakın Çalışma", "Toplayıcı Sıfırla", "Arıza Sil", "Kullanılmıyor", "Kullanılmıyor", "Kalibrasyon Başla", "Kalibrasyon Kabul", "Kalıbrasyon Red", "Kalibrasyon Durduruldu" };
//            string[] outputNames = { "Tolerans Arıza", "Ağırlık Arıza", "Acil Dur", "Band Kaydı Arızası", "Hız Arıza", "Debi Arıza", "Ön Besleyici Çalış", "Çalış", "Kullanılmıyor", "Patinaj Arıza", "Kalibrasyon Red/Kabul Bekleme", "Kalibrasyon Tolerans içinde", "Kalibrasyon Başladı", "Motor Arıza", "Sıfır Arıza", "Remote" };
//            string[] statusNames = { "Sistem Çalışıyor", "Yakın Çalışma Durumu", "Manuel Durumda", "Kalibrasyon Durumunda", "Band Kaydı Arızası", "Acil Dur Durumu", "Ağırlık Arıza", "Sıfır Arıza", "Hız Arıza", "Debi Arıza", "Normal Çalışma İçin Bir Engel Var", "Tolerans Arıza", "Sayıcı Darbesi", "Motor Arıza", "Kullanılmıyor", "Kullanılmıyor" };
//            string[] bunkerNames = { "Kalibrasyon İşleminde", "Kalibrasyon Tolerans İçinde", "Kalibrasyon Tolerans Dışında Ret/Kabul Bekleniyor", "Bunker Ağırlık Arıza", "Bunker Seviyesi Alt Seviye Altında", "Bunker Seviyesi Üst Seviyenin Üzerinde", "Kullanılmıyor", "Kullanılmıyor" };

//            for (int i = 0; i < 16; i++) { ledsInput[i] = CreateLed(gridInputs, inputNames[i]); }
//            for (int i = 0; i < 16; i++) { ledsOutput[i] = CreateLed(gridOutputs, outputNames[i]); }
//            for (int i = 0; i < 16; i++) { ledsStatus[i] = CreateLed(gridStatus, statusNames[i]); }
//            for (int i = 0; i < 8; i++) { ledsBunker[i] = CreateLed(gridBunkerStatus, bunkerNames[i]); }
//        }

//        private Border CreateLed(UniformGrid parent, string text)
//        {
//            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

//            Border led = new Border
//            {
//                Width = 24,
//                Height = 12,
//                CornerRadius = new CornerRadius(3),
//                Background = colorOff,
//                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 27, 33)),
//                BorderThickness = new Thickness(1),
//                Margin = new Thickness(0, 0, 10, 0),
//                VerticalAlignment = VerticalAlignment.Center
//            };

//            TextBlock tb = new TextBlock
//            {
//                Text = text,
//                Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
//                FontSize = 14,
//                FontWeight = FontWeights.Bold,
//                VerticalAlignment = VerticalAlignment.Center,
//                TextWrapping = TextWrapping.Wrap
//            };

//            if (text == "Kullanılmıyor" || text == "---")
//            {
//                tb.Foreground = colorSilver;
//                tb.FontWeight = FontWeights.Normal;
//            }

//            sp.Children.Add(led);
//            sp.Children.Add(tb);
//            parent.Children.Add(sp);
//            return led;
//        }

//        // --- GÜVENLİ DOUBLE DÖNÜŞTÜRME ---
//        private double ParseDoubleSafe(string val)
//        {
//            if (string.IsNullOrWhiteSpace(val)) return 0;
//            string cleaned = val.Replace(" kg", "").Replace("", "").Replace(",", ".");
//            double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result);
//            return result;
//        }

//        // --- BAĞLANTI YÖNETİMİ ---
//        private async void btnConnect_Click(object sender, RoutedEventArgs e)
//        {
//            if (isCommunicationActive)
//            {
//                isCommunicationActive = false;
//                await Task.Delay(250);
//                if (serial != null) serial.Close();

//                db.OlayKaydet("Kullanıcı bağlantıyı manuel olarak kesti.", "BILGI", activeSession);
//                activeSession = "";

//                btnConnect.Content = "Sisteme Bağlan";
//                btnConnect.Background = colorGreen;
//                btnConnect.Foreground = new SolidColorBrush(Color.FromRgb(24, 27, 33));
//                lblHeartbeat.Fill = colorRed;

//                ResetUI();
//                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Bağlantı Kesildi.");
//                return;
//            }

//            try
//            {
//                int baud = int.Parse(txtBaudRate.Text);
//                serial = new SerialPortManager(txtPort.Text, baud, System.IO.Ports.Parity.Even, 8, System.IO.Ports.StopBits.One);
//                master = new ModbusMaster(serial);
//                serial.Open();

//                if (serial.IsOpen())
//                {
//                    isCommunicationActive = true;
//                    consecutiveErrorCount = 0;
//                    activeSession = "RUN-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

//                    btnConnect.Content = "Bağlantıyı Kes";
//                    btnConnect.Background = colorRed;
//                    btnConnect.Foreground = Brushes.White;

//                    db.OlayKaydet($"{txtPort.Text} üzerinden bağlantı sağlandı.", "BILGI", activeSession);
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {txtPort.Text} Bağlandı. Oturum: {activeSession}");

//                    _ = CommunicationLoop();
//                }
//            }
//            catch (Exception ex)
//            {
//                isCommunicationActive = false; activeSession = "";
//                MessageBox.Show($"Bağlantı Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private async Task CommunicationLoop()
//        {
//            while (isCommunicationActive)
//            {
//                if (master != null && master.IsOpen() && !isWriting)
//                {
//                    try
//                    {
//                        byte slaveID = byte.Parse(txtSlaveId.Text);
//                        byte[] data = await master.ReadHoldingRegistersAsync(slaveID, 0, 35);

//                        if (data != null && data.Length >= 73)
//                        {
//                            consecutiveErrorCount = 0;
//                            Dispatcher.Invoke(() =>
//                            {
//                                UpdateUIWithData(data);
//                                lblHeartbeat.Fill = (lblHeartbeat.Fill == colorGreen) ? colorOff : colorGreen;
//                            });
//                        }
//                        else { consecutiveErrorCount++; }
//                    }
//                    catch { consecutiveErrorCount++; }

//                    if (consecutiveErrorCount >= 100)
//                    {
//                        Dispatcher.Invoke(() =>
//                        {
//                            if (consecutiveErrorCount == 100)
//                            {
//                                lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] !!! BAĞLANTI KOPTU !!!");
//                                db.OlayKaydet("Haberleşme zaman aşımı: 100 ardışık hata oluştu.", "ALARM", activeSession);
//                                lblHeartbeat.Fill = colorRed;
//                                ResetUI();
//                            }
//                        });
//                    }
//                }
//                await Task.Delay(150);
//            }
//        }

//        // --- VERİLERİ EKRANA DAĞITMA ---
//        private void UpdateUIWithData(byte[] data)
//        {
//            ushort GetReg(int addr) => (ushort)((data[3 + (addr * 2)] << 8) | data[3 + (addr * 2) + 1]);

//            try
//            {
//                // 1. ANA SAYISAL GÖSTERGELER
//                lblSetPoint.Text = GetReg(1).ToString() + " ton/sa";
//                lblCurrentWeight.Text = GetReg(4).ToString() + " kg";
//                lblBeltSpeed.Text = GetReg(5).ToString();
//                lblControlOutput.Text = GetReg(6).ToString();
//                lblWeightSetRead.Text = GetReg(11).ToString() + " ton";
//                lblTotalPassed.Text = GetReg(12).ToString() + " ton";
//                lblPreFeederOutput.Text = GetReg(13).ToString();

//                // 2. ONDALIKLI DEBİ HESAPLARI
//                int decimalPoint = GetReg(17);
//                double divider = Math.Pow(10, decimalPoint);

//                lblSetPoint.Text= (GetReg(1) / divider).ToString("F" + decimalPoint) + " ton/sa";
//                lblFlowRate.Text = (GetReg(3) / divider).ToString("F" + decimalPoint) + " ton/sa";
//                lblAverageFlow.Text = (GetReg(2) / divider).ToString("F" + decimalPoint) + " ton/sa";
//                lblDecimalPoint.Text = decimalPoint.ToString();

//                // 3. 32-BIT TOPLAYICILAR
//                lblTotalWeight1.Text = (GetReg(7) | (GetReg(8) << 16)).ToString() + " ton";
//                lblTotalWeight2.Text = (GetReg(9) | (GetReg(10) << 16)).ToString() + " ton";

//                // 4. I/O LAMBALARI
//                ushort inV = GetReg(14);
//                ushort inO = GetReg(15);
//                ushort inS = GetReg(16);
//                ushort bnkrSt = chkBunkerEnabled.IsChecked == true ? GetReg(21) : (ushort)0;

//                for (int i = 0; i < 16; i++)
//                {
//                    bool isInput = (inV & (1 << i)) != 0;
//                    Brush vColor = colorGreen;
//                    if (i == 3 || i == 5 || i == 14 || i == 15) vColor = colorRed;
//                    if (i == 10 || i == 11) vColor = colorSilver;
//                    ledsInput[i].Background = isInput && (i != 10 && i != 11) ? vColor : colorOff;

//                    bool isOutput = (inO & (1 << i)) != 0;
//                    Brush oColor = colorGreen;
//                    if ((i >= 0 && i <= 5) || i == 9 || i == 13) oColor = colorRed;
//                    if (i == 10) oColor = colorYellow;
//                    if (i == 8) oColor = colorSilver;
//                    ledsOutput[i].Background = isOutput && (i != 8) ? oColor : colorOff;

//                    bool isStatus = (inS & (1 << i)) != 0;
//                    Brush sColor = colorGreen;
//                    if (i == 3 || i == 5 || i == 10 || i == 11) sColor = colorRed;
//                    if (i >= 14) sColor = colorSilver;
//                    ledsStatus[i].Background = isStatus && (i < 14) ? sColor : colorOff;
//                }

//                // 5. BUNKER SİSTEMİ
//                if (chkBunkerEnabled.IsChecked == true)
//                {
//                    lblBunkerWeight.Text = GetReg(18).ToString() + " ton";
//                    lblBunkerWeightSetRead.Text = GetReg(19).ToString() + " ton";
//                    lblBunkerControlOutput.Text = GetReg(20).ToString();
//                    lblBunkerDischargedWeight.Text = GetReg(22).ToString() + " kg";
//                    lblBeltWeighedWeight.Text = GetReg(23).ToString() + " kg";

//                    for (int i = 0; i < 8; i++)
//                    {
//                        bool isBunker = (bnkrSt & (1 << i)) != 0;
//                        Brush bColor = colorGreen;
//                        if (i == 3) bColor = colorRed;
//                        if (i >= 6) bColor = colorSilver;
//                        ledsBunker[i].Background = isBunker && (i < 6) ? bColor : colorOff;
//                    }
//                }

//                // 6. TEXTBOX VE BUTON SENKRONİZASYONU
//                if (!isWriting)
//                {
//                    if (!is30Changed && !txtTargetDose.IsFocused) txtTargetDose.Text = GetReg(30).ToString();
//                    if (!is33Changed && !txtWeightSet.IsFocused) txtWeightSet.Text = GetReg(33).ToString();
//                    if (chkBunkerEnabled.IsChecked == true && !is34Changed && !txtBunkerWeightSet.IsFocused)
//                        txtBunkerWeightSet.Text = GetReg(34).ToString();

//                    commandData31 = GetReg(31);
//                    commandToSend32 = GetReg(32);
//                }

//                btnSystemRun.Background = (commandData31 & 0x0001) != 0 ? colorGreen : colorOff;
//                btnMotorRun.Background = (commandData31 & 0x0004) != 0 ? colorGreen : colorOff;
//                btnPreSystemRun.Background = (commandData31 & 0x0002) != 0 ? colorGreen : colorOff;
//                btnMotorFault.Background = (commandData31 & 0x0008) != 0 ? colorRed : colorOff;
//                btnPreFeederRun.Background = (commandData31 & 0x0010) != 0 ? colorGreen : colorOff;
//                btnPreFeederFault.Background = (commandData31 & 0x0020) != 0 ? colorRed : colorOff;
//                btnRemoteOperation.Background = (commandData31 & 0x0040) != 0 ? colorGreen : colorOff;
//                btnLocalOperation.Background = (commandData31 & 0x0080) != 0 ? colorGreen : colorOff;

//                btnResetTotalizer.Background = (commandToSend32 & 0x0001) != 0 ? colorYellow : colorOff;
//                btnResetFaults.Background = (commandToSend32 & 0x0002) != 0 ? colorYellow : colorOff;
//                btnStartCalibration.Background = (commandToSend32 & 0x0010) != 0 ? colorYellow : colorOff;
//                btnAcceptCalibration.Background = (commandToSend32 & 0x0020) != 0 ? colorGreen : colorOff;
//                btnRejectCalibration.Background = (commandToSend32 & 0x0040) != 0 ? colorRed : colorOff;
//                btnStopFilling.Background = (commandToSend32 & 0x0080) != 0 ? colorRed : colorOff;

//                CheckAndLogAlarms(inV, inO, inS, bnkrSt);
//            }
//            catch { }
//        }

//        // --- ALARM DENETİM VE DB KAYIT ---
//        private void CheckAndLogAlarms(ushort inV, ushort inO, ushort inS, ushort bnkrSt)
//        {
//            CheckAndLog(inS, oldInS, 0x0010, "Band Kaydı Arıza");
//            CheckAndLog(inS, oldInS, 0x0020, "Acil Dur Durumu");
//            CheckAndLog(inS, oldInS, 0x0040, "Ağırlık Arıza");
//            CheckAndLog(inS, oldInS, 0x0080, "Sıfır Arıza");
//            CheckAndLog(inS, oldInS, 0x0100, "Hız Arıza");
//            CheckAndLog(inS, oldInS, 0x0200, "Debi Arıza");
//            CheckAndLog(inS, oldInS, 0x0400, "Normal Çalışma Engel");
//            CheckAndLog(inS, oldInS, 0x0800, "Tolerans Arıza");
//            CheckAndLog(inS, oldInS, 0x2000, "Motor Arıza");

//            if (chkBunkerEnabled.IsChecked == true)
//                CheckAndLog(bnkrSt, oldBnkrSt, 0x0008, "Bunker Ağırlık Arızası");

//            oldInV = inV; oldInO = inO; oldInS = inS; oldBnkrSt = bnkrSt;
//        }

//        private void CheckAndLog(ushort current, ushort old, ushort mask, string alarmName)
//        {
//            bool hasErrorNow = (current & mask) != 0;
//            bool hadErrorBefore = (old & mask) != 0;

//            if (hasErrorNow && !hadErrorBefore)
//            {
//                db.OlayKaydet(alarmName + " BAŞLADI", "ALARM", activeSession);
//                Dispatcher.Invoke(() => lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {alarmName} Başladı."));
//            }
//            else if (!hasErrorNow && hadErrorBefore)
//            {
//                db.OlayKaydet(alarmName + " GİDERİLDİ", "ALARM", activeSession);
//                Dispatcher.Invoke(() => lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {alarmName} Giderildi."));
//            }
//        }

//        private void SavePeriodicData()
//        {
//            if (!isCommunicationActive) return;
//            try
//            {
//                double flowRate = ParseDoubleSafe(lblFlowRate.Text);
//                double currentWeight = ParseDoubleSafe(lblCurrentWeight.Text);
//                double beltSpeed = ParseDoubleSafe(lblBeltSpeed.Text);
//                double total1 = ParseDoubleSafe(lblTotalWeight1.Text);
//                double total2 = ParseDoubleSafe(lblTotalWeight2.Text);
//                double bunkerWeight = ParseDoubleSafe(lblBunkerWeight.Text);

//                db.VeriKaydet(flowRate, currentWeight, beltSpeed, total1, total2, bunkerWeight, commandData31, commandToSend32, oldInV, oldInO, oldInS, activeSession);
//            }
//            catch { }
//        }

//        // --- GÜVENLİ YAZMA VE BUTON OLAYLARI ---
//        private async Task SafeWriteAsync(ushort address, ushort value)
//        {
//            if (master == null || !isCommunicationActive) return;
//            try
//            {
//                isWriting = true;
//                await Task.Delay(50);
//                byte slaveID = byte.Parse(txtSlaveId.Text);
//                await master.WriteHoldingRegisterAsync(slaveID, address, value);
//                await Task.Delay(100);
//            }
//            finally { isWriting = false; }
//        }

//        // Komut 1 
//        private async void btnSystemRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 1; await SafeWriteAsync(31, commandData31); }
//        private async void btnPreSystemRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 2; await SafeWriteAsync(31, commandData31); }
//        private async void btnMotorRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 4; await SafeWriteAsync(31, commandData31); }
//        private async void btnMotorFault_Click(object sender, RoutedEventArgs e) { commandData31 ^= 8; await SafeWriteAsync(31, commandData31); }
//        private async void btnPreFeederRun_Click(object sender, RoutedEventArgs e) { commandData31 ^= 16; await SafeWriteAsync(31, commandData31); }
//        private async void btnPreFeederFault_Click(object sender, RoutedEventArgs e) { commandData31 ^= 32; await SafeWriteAsync(31, commandData31); }
//        private async void btnRemoteOperation_Click(object sender, RoutedEventArgs e) { commandData31 ^= 64; await SafeWriteAsync(31, commandData31); }
//        private async void btnLocalOperation_Click(object sender, RoutedEventArgs e) { commandData31 ^= 128; await SafeWriteAsync(31, commandData31); }

//        // Komut 2 (Pulse Mantığı)
//        private async void btnResetTotalizer_Click(object sender, RoutedEventArgs e)
//        {
//            commandToSend32 |= 1;
//            await SafeWriteAsync(32, commandToSend32);
//            await Task.Delay(500);
//            commandToSend32 = (ushort)(commandToSend32 & ~1);
//            await SafeWriteAsync(32, commandToSend32);
//        }

//        private async void btnResetFaults_Click(object sender, RoutedEventArgs e)
//        {
//            commandToSend32 |= 2;
//            await SafeWriteAsync(32, commandToSend32);
//            await Task.Delay(500);
//            commandToSend32 = (ushort)(commandToSend32 & ~2);
//            await SafeWriteAsync(32, commandToSend32);
//        }

//        private async void btnStartCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 16; await SafeWriteAsync(32, commandToSend32); }
//        private async void btnAcceptCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 32; await SafeWriteAsync(32, commandToSend32); }
//        private async void btnRejectCalibration_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 64; await SafeWriteAsync(32, commandToSend32); }
//        private async void btnStopFilling_Click(object sender, RoutedEventArgs e) { commandToSend32 ^= 128; await SafeWriteAsync(32, commandToSend32); }

//        // 🔥 YENİ: Sadece kullanıcının değiştirdiği değerleri gönderen akıllı Ayar Gönderme Metodu
//        private async void btnSendSettings_Click(object sender, RoutedEventArgs e)
//        {
//            if (!isCommunicationActive) { MessageBox.Show("Önce cihaza bağlanın!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
//            try
//            {
//                bool ayarGonderildi = false;

//                // Sadece kullanıcı dokunduysa ve değiştirdiyse gönder
//                if (is30Changed && ushort.TryParse(txtTargetDose.Text, out ushort v30))
//                {
//                    db.AyarKaydet("Hedef Dozaj", "0", v30.ToString(), activeSession);
//                    await SafeWriteAsync(30, v30);
//                    ayarGonderildi = true;
//                }

//                if (is33Changed && ushort.TryParse(txtWeightSet.Text, out ushort v33))
//                {
//                    db.AyarKaydet("Ağırlık Set", "0", v33.ToString(), activeSession);
//                    await SafeWriteAsync(33, v33);
//                    ayarGonderildi = true;
//                }

//                if (chkBunkerEnabled.IsChecked == true && is34Changed && ushort.TryParse(txtBunkerWeightSet.Text, out ushort v34))
//                {
//                    db.AyarKaydet("Bunker Ağırlık Set", "0", v34.ToString(), activeSession);
//                    await SafeWriteAsync(34, v34);
//                    ayarGonderildi = true;
//                }
//                await Task.Delay(600);

//                // Değerler gönderildikten sonra kilitleri aç ki cihazdan gelen güncel durum ekrana düşsün
//                is30Changed = false;
//                is33Changed = false;
//                is34Changed = false;

//                if (ayarGonderildi)
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Değiştirilen ayarlar cihaza yazıldı.");
//                else
//                    lstLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Değiştirilen yeni bir ayar bulunamadı.");
//            }
//            catch { }
//        }

//        // 🔥 BUNKER CHECKBOX KONTROLÜ: PASİF OLUNCA HER ŞEYİ (YAZILAR DAHİL) SOLUKLAŞTIR
//        private void chkBunkerEnabled_CheckedChanged(object sender, RoutedEventArgs e)
//        {
//            if (pnlBunker != null)
//            {
//                bool isActive = chkBunkerEnabled.IsChecked == true;
//                pnlBunker.IsEnabled = isActive;

//                // Bunker Set Kutusunun Dinamik Kontrolü (Renkler ve Yazı)
//                if (txtBunkerWeightSet != null)
//                {
//                    txtBunkerWeightSet.IsEnabled = isActive;
//                    txtBunkerWeightSet.Background = isActive ? new SolidColorBrush(Color.FromRgb(33, 37, 45)) : new SolidColorBrush(Color.FromRgb(49, 55, 67));
//                    txtBunkerWeightSet.Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(92, 99, 112));

//                    if (!isActive) txtBunkerWeightSet.Text = "0"; // Pasifse değer sıfırlanır
//                }

//                // Pasif duruma geçince tüm rakamların ve yazıların rengini soluk (Gri) yap
//                SolidColorBrush fadedText = new SolidColorBrush(Color.FromRgb(92, 99, 112)); // Soluk Gri
//                SolidColorBrush activeText = new SolidColorBrush(Color.FromRgb(171, 178, 191)); // Normal Canlı Yazı

//                if (isActive)
//                {
//                    // Aktifse orijinal canlı XAML renklerini geri veriyoruz
//                    lblBunkerWeight.Foreground = new SolidColorBrush(Color.FromRgb(229, 192, 123)); // #E5C07B
//                    lblBunkerWeightSetRead.Foreground = new SolidColorBrush(Color.FromRgb(229, 192, 123)); // #E5C07B
//                    lblBunkerControlOutput.Foreground = new SolidColorBrush(Color.FromRgb(209, 154, 102)); // #D19A66
//                    lblBunkerDischargedWeight.Foreground = new SolidColorBrush(Color.FromRgb(198, 120, 221)); // #C678DD
//                    lblBeltWeighedWeight.Foreground = new SolidColorBrush(Color.FromRgb(86, 182, 194)); // #56B6C2
//                }
//                else
//                {
//                    // Pasif olunca tüm ekrandaki bunker rakamlarını sıfırla ve soluk yap
//                    lblBunkerWeight.Text = "0 ton";
//                    lblBunkerWeightSetRead.Text = "0 ton";
//                    lblBunkerControlOutput.Text = "0";
//                    lblBunkerDischargedWeight.Text = "0 kg";
//                    lblBeltWeighedWeight.Text = "0 kg";

//                    lblBunkerWeight.Foreground = fadedText;
//                    lblBunkerWeightSetRead.Foreground = fadedText;
//                    lblBunkerControlOutput.Foreground = fadedText;
//                    lblBunkerDischargedWeight.Foreground = fadedText;
//                    lblBeltWeighedWeight.Foreground = fadedText;

//                    // Tüm Bunker LED lambalarını (Status) zorla söndür
//                    if (ledsBunker != null)
//                    {
//                        foreach (var led in ledsBunker)
//                        {
//                            if (led != null) led.Background = colorOff;
//                        }
//                    }
//                }

//                // Bunker Durum (Status) listesindeki lambaların yanındaki yazıları da soluklaştır
//                if (gridBunkerStatus != null)
//                {
//                    foreach (UIElement item in gridBunkerStatus.Children)
//                    {
//                        if (item is StackPanel sp)
//                        {
//                            foreach (UIElement child in sp.Children)
//                            {
//                                // Sadece "Kullanılmıyor" yazmayan geçerli durum etiketlerini soluklaştır/canlandır
//                                if (child is TextBlock tb && tb.Text != "Kullanılmıyor" && tb.Text != "---")
//                                {
//                                    tb.Foreground = isActive ? activeText : fadedText;
//                                }
//                            }
//                        }
//                    }
//                }

//                lstLogs.Items.Insert(0, isActive ? $"[{DateTime.Now:HH:mm:ss}] Bunker sistemi aktif." : $"[{DateTime.Now:HH:mm:ss}] Bunker sistemi pasif modda.");
//            }
//        }

//        private void btnReports_Click(object sender, RoutedEventArgs e)
//        {
//            RaporWindow raporEkrani = new RaporWindow(activeSession);
//            raporEkrani.ShowDialog();
//        }

//        private void ResetUI()
//        {
//            lblSetPoint.Text = "0 ton/sa"; lblFlowRate.Text = "0.0 ton/sa"; lblCurrentWeight.Text = "0.00 kg";
//            lblAverageFlow.Text = "0.0 ton/sa"; lblBeltSpeed.Text = "0"; lblTotalPassed.Text = "0 ton";
//            lblTotalWeight1.Text = "0 ton"; lblTotalWeight2.Text = "0 ton"; lblWeightSetRead.Text = "0 ton";
//            lblBunkerWeight.Text = "0 ton"; lblPreFeederOutput.Text = "0"; lblDecimalPoint.Text = "0";
//            lblControlOutput.Text = "0"; lblBunkerControlOutput.Text = "0"; lblBunkerWeightSetRead.Text = "0 ton";
//            lblBunkerDischargedWeight.Text = "0 kg"; lblBeltWeighedWeight.Text = "0 kg";

//            foreach (var b in ledsInput) b.Background = colorOff;
//            foreach (var b in ledsOutput) b.Background = colorOff;
//            foreach (var b in ledsStatus) b.Background = colorOff;
//            foreach (var b in ledsBunker) b.Background = colorOff;

//            commandData31 = 0; commandToSend32 = 0;
//            btnSystemRun.Background = colorOff; btnMotorRun.Background = colorOff;
//            btnPreSystemRun.Background = colorOff; btnMotorFault.Background = colorOff;
//            btnPreFeederRun.Background = colorOff; btnPreFeederFault.Background = colorOff;
//            btnRemoteOperation.Background = colorOff; btnLocalOperation.Background = colorOff;
//            btnResetTotalizer.Background = colorOff; btnResetFaults.Background = colorOff;
//            btnStartCalibration.Background = colorOff; btnAcceptCalibration.Background = colorOff;
//            btnRejectCalibration.Background = colorOff; btnStopFilling.Background = colorOff;
//        }
//    }
//}
