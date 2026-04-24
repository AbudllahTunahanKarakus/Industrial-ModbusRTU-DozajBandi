# 🏭 Endüstriyel Dozaj Bant Kantarı SCADA Sistemi

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Modbus](https://img.shields.io/badge/Protocol-Modbus_RTU-blue?style=for-the-badge)
![SQL Server](https://img.shields.io/badge/Database-MS_SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)

Endüstriyel tartım ve dozajlama sistemleri (Elimko AC-10 vb.) için geliştirilmiş, **Modbus RTU** haberleşme protokolü üzerinde çalışan, yüksek performanslı ve asenkron bir SCADA (Supervisory Control and Data Acquisition) arayüzüdür. 

Bu proje, bir endüstriyel tesisin sahadaki cihazlarını gerçek zamanlı olarak izlemesini, kontrol etmesini ve tüm üretim/arıza verilerini otonom bir şekilde veritabanına loglamasını sağlar.

---

## 🚀 Temel Özellikler

* **Gerçek Zamanlı Asenkron Okuma (Polling):** Arayüzü dondurmadan arka planda çalışan Task-tabanlı döngü ile cihazdan 150 ms'de bir veri çekilir ve UI güncellenir.
* **Evrensel Ölçekleme (Dynamic Scaling):** Cihazın gönderdiği ham tamsayı verileri (örn: `125`), cihazdan okunan "Noktanın Yeri" parametresine göre dinamik olarak ondalıklı değerlere (örn: `12.5`) çevrilerek gösterilir ve cihaza yazılırken tekrar tamsayıya encode edilir.
* **Bitwise (Bit Düzeyinde) Durum Kontrolü:** Cihazın 16-bitlik tek bir I/O register'ından (InV, InO, InS) gelen veri, bit maskeleme `(inV & (1 << i)) != 0` yöntemiyle parçalanarak 16 farklı lambanın ve alarmın durumu aynı anda tespit edilir.
* **Çakışmasız Komut Gönderimi (XOR Toggle):** Motora veya besleyiciye komut gönderirken, diğer çalışan sistemleri durdurmamak için Bitwise XOR (`^=`) operatörü kullanılarak cihazın `Holding Register` hafızasına güvenli yazma işlemi yapılır.
* **Otonom Veri ve Alarm Loglama:** Sistem her bağlandığında benzersiz bir oturum (Session ID) oluşturur. Periyodik üretim verilerini ve cihazda oluşan anlık bit tabanlı alarmları MS SQL Server üzerine ilişkisel olarak kaydeder.
* **Fail-Safe (Hata Koruması):** Cihaz bağlantısı koptuğunda veya kablo çıkarıldığında program çökmez. Ardışık 100 hatadan (Timeout) sonra sistem kendini güvenli moda alır ve portu kapatır.

---

## 🏗️ Sistem Mimarisi ve Kullanılan Teknolojiler

* **Geliştirme Ortamı:** Visual Studio 2022
* **Programlama Dili:** C# (.NET)
* **Masaüstü Arayüzü:** WPF (Windows Presentation Foundation) - XAML tabanlı modern, aydınlık/karanlık tema destekli endüstriyel UI tasarımı.
* **Haberleşme Kütüphanesi:** ModbusLibrary (Kendi Yazdığım Kütüphane FNC03,FNC06,FN16) (Seri Port üzerinden RS485/RS232 iletişimi)
* **Veritabanı:** MS SQL Server (ADO.NET ile doğrudan bağlantı)

---

## 📸 Ekran Görüntüleri
WPF Ekran Son Hali

| İzleme Ekranı | Alarmlar |
| :---: | :---: |
| <img width="1920" height="1032" alt="Ekrannnnnnnnn" src="https://github.com/user-attachments/assets/8d9130e0-7971-4815-944d-52790d834b7c" /> | <img width="1036" height="643" alt="Alarm ekrannn" src="https://github.com/user-attachments/assets/836a99b4-3e2b-455e-b8f4-a93e3c7f7b3e" /> |

---
## 📸 Ekran Görüntüleri
Form Ekranı İlk Çalışma

| İzleme Ekranı | Alarmlar |
| :---: | :---: |
| <img width="1557" height="676" alt="Ekran görüntüsü 2026-04-24 115955" src="https://github.com/user-attachments/assets/e8584deb-0bc4-40eb-95eb-8eaa0f6f5706" />| <img width="936" height="543" alt="Form EKrannn" src="https://github.com/user-attachments/assets/0b175b0b-1f2a-48f8-b5b0-13d02290009f" />|

---

## 💻 Kurulum ve Kullanım

### Gereksinimler
* Windows İşletim Sistemi (Windows 10/11)
* .NET Framework veya .NET Core Runtime
* Microsoft SQL Server Express (Veritabanı işlemleri için)
* RS485 to USB Dönüştürücü (Fiziksel test için)

### Çalıştırma Adımları
1. Visual Studio ile .sln dosyasını açın.
2. Projeyi Release moduna alın ve derleyin (Build).
3. Uygulamayı çalıştırın.
4. Cihazın bağlı olduğu COM Portunu, Baud Rate (örn: 9600) ve Slave ID değerini girerek Sisteme Bağlan butonuna tıklayın.
* ** Projeyi bilgisayarınıza klonlayın:
   ```bash
   git clone [https://github.com/KULLANICI_ADIN/Industrial-Modbus-SCADA.git](https://github.com/KULLANICI_ADIN/Industrial-Modbus-SCADA.git)
### Cihaz Yok ise
* **VSPE uygulamasını indirerek çift port açılır bilgisayarınızın bağlanacağı ve sımülasyon uygulmasının bağlanacağı.
* <img width="43" height="42" alt="Ekran görüntüsü 2026-04-24 115544" src="https://github.com/user-attachments/assets/d7927c07-ca3d-4373-9f9c-e820c6a00989" />

* **mod_RSsim indirirsiniz haberleşmek istediğiniz cihaz gibi davranır diğer portu da sımulasyona bağlarsınız.
* <img width="48" height="47" alt="Ekran görüntüsü 2026-04-24 115644" src="https://github.com/user-attachments/assets/c3a269b0-fd49-4967-a129-2fb17a320409" />

* **Test edebilirsiniz COM Portunu, Baud Rate ve Slave ID değerlerini doğru girerek Sisteme Bağlan butonuna tıklayın.



