# DeviceService Servis Takip Sistemi

Telefon ve elektronik cihaz servisleri için geliştirilmiş bir servis yönetim uygulamasıdır. Çözüm; ASP.NET Core Web API, SQL Server/LocalDB, Entity Framework Core, Blazor Web ve .NET MAUI Blazor Hybrid istemcilerinden oluşur.

## Özellikler

- Müşteri ve servis hesabı kaydı
- JWT tabanlı kimlik doğrulama ve rol yetkilendirmesi
- Müşteri, cihaz ve servis fişi yönetimi
- Otomatik servis fişi numarası ve güvenli takip kodu oluşturma
- Durum geçmişi, not ve tahmini ücret takibi
- Teslimde teslim alan kişi adı, cihaz fotoğrafı ve kimlik belgesi fotoğrafı ile teslim kanıtı
- Yetkili servis için PDF servis fişi indirme
- Takip doğrulaması için telefon numarasının son dört hanesi
- Hatalı takip doğrulamasında deneme sınırı ve geçici kilit
- Servis fişi ve şifre sıfırlama e-postaları için SMTP desteği
- Yeni cihazdan girişte e-posta bildirimi
- Şifre sıfırlama bağlantısı ve şifre değiştirme
- Şifre değiştirildiğinde veya tüm cihazlardan çıkış yapıldığında eski oturumların iptali
- Oturum bitmeden uyarı ve hareketsizlikte otomatik çıkış
- Web, Windows ve Android istemcileri
- İstemcide JWT oturumunu saklayan "Beni hatırla" seçeneği

## Proje Yapısı

- `DeviceService.API`: REST API, JWT, Swagger ve SMTP entegrasyonu
- `DeviceService.Core`: Entity, enum ve arayüzler
- `DeviceService.Data`: EF Core DbContext ve migration'lar
- `DeviceService.Services`: Ortak API istemcisi ve iş mantığı
- `DeviceService.Shared`: Web ve MAUI tarafından kullanılan Razor bileşenleri
- `DeviceService.Web`: Blazor Web uygulaması
- `DeviceService.Maui`: .NET MAUI Blazor Hybrid uygulaması

## Gereksinimler

- .NET 10 SDK
- SQL Server LocalDB veya SQL Server
- Android geliştirme için .NET MAUI workload ve Android SDK
- E-posta özelliği kullanılacaksa SMTP hesabı

## Güvenli Yerel Yapılandırma

Gerçek JWT anahtarı, servis kayıt kodu, SMTP anahtarı ve yayın adresi repoya yazılmamalıdır. Yerel geliştirmede API ayarları için `dotnet user-secrets` kullanılabilir.

Rastgele bir JWT anahtarı oluştur:

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 48
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $jwtKey --project .\DeviceService.API\DeviceService.API.csproj
$rng.Dispose()
```

Servis hesabı kaydı için bir kayıt kodu tanımla:

```powershell
dotnet user-secrets set "Registration:ServiceCode" "KENDI-GUCLU-KAYIT-KODUN" --project .\DeviceService.API\DeviceService.API.csproj
```

SMTP ve e-posta bağlantıları için gerekli ayarlar:

```powershell
dotnet user-secrets set "Email:Host" "smtp.example.com" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:Port" "587" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:UserName" "smtp-kullanici" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:Password" "smtp-parolasi-veya-anahtari" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:FromAddress" "no-reply@example.com" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:PublicAppBaseUrl" "https://web-adresin.example.com" --project .\DeviceService.API\DeviceService.API.csproj
```

`Email:PublicAppBaseUrl`, şifre sıfırlama e-postasındaki bağlantının açılacağı genel web adresidir.

## Veritabanı

Varsayılan bağlantı SQL Server LocalDB kullanır. Şemayı oluşturmak veya güncellemek için:

```powershell
dotnet ef database update --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
```

Tüm yerel kayıtları silip boş şemayı yeniden oluşturmak için:

```powershell
dotnet ef database drop --force --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
dotnet ef database update --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
```

## Çalıştırma

API:

```powershell
dotnet run --project .\DeviceService.API\DeviceService.API.csproj --launch-profile http
```

Swagger geliştirme ortamında `http://localhost:5113/swagger` adresinde açılır.

Web:

```powershell
dotnet run --project .\DeviceService.Web\DeviceService.Web.csproj
```

Windows MAUI:

```powershell
dotnet run --project .\DeviceService.Maui\DeviceService.Maui.csproj -f net10.0-windows10.0.19041.0
```

## Android APK

Android uygulamasının API adresi kaynak kodda tutulmaz. APK oluştururken `DeviceServiceApiBaseUrl` parametresiyle verilir. Adresin sonunda `/` bulunmalıdır.

```powershell
dotnet publish .\DeviceService.Maui\DeviceService.Maui.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=apk -p:DeviceServiceApiBaseUrl=https://api-adresin.example.com/
```

APK çıktısı:

```text
DeviceService.Maui\bin\Release\net10.0-android\publish\
```

## Güvenlik Notları

- Tüm API endpoint'leri varsayılan olarak kimlik doğrulaması ister.
- Servis yönetimi yalnızca `Service` rolüne açıktır.
- Müşteriler yalnızca kendi servis fişlerini görüntüleyebilir.
- Giriş, kayıt ve şifre sıfırlama istekleri oran sınırlamasına sahiptir.
- Parolalar PBKDF2-SHA256, rastgele salt ve 210.000 iterasyonla hashlenir.
- Takip tokenları kriptografik olarak rastgele üretilir.
- Telefon numarasının tamamı takip istemcisine gönderilmez.
- Şifre değişimi ve "Tüm Oturumları Kapat" işlemi eski JWT oturumlarını geçersiz kılar.
- SMTP ayarları yapılandırılmadıysa e-posta gönderimi başarısız olur; servis fişi kaydı korunur.
- Teslim kanıtı görselleri `DeviceService.API/App_Data/DeliveryEvidence` altında tutulur; uygulama tarafından public olarak yayınlanmaz ve Git tarafından yok sayılır.
- "Teslim Edildi" durumuna geçmek için teslim alan kişi adı ile iki teslim görseli API tarafında da zorunludur.

## GitHub'a Yüklemeden Önce

- `.env`, `bin`, `obj`, sertifika, anahtar ve yerel yayın çıktıları Git dışındadır.
- Gerçek parola, API anahtarı, e-posta adresi veya kişisel tünel URL'si eklemeyin.
- Değişiklikleri doğrulamak için:

```powershell
dotnet build .\DeviceService.API\DeviceService.API.csproj
dotnet build .\DeviceService.Web\DeviceService.Web.csproj
dotnet build .\DeviceService.Maui\DeviceService.Maui.csproj -f net10.0-windows10.0.19041.0
```