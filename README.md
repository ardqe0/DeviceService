# DeviceService Servis Takip Sistemi

Telefon ve elektronik cihaz servisleri için geliştirilmiş servis yönetim uygulamasıdır. ASP.NET Core Web API, SQL Server, Entity Framework Core, Blazor Web ve .NET MAUI Blazor Hybrid istemcilerinden oluşur.

## Özellikler

- Müşteri ve servis hesabı kaydı
- JWT tabanlı kimlik doğrulama ve rol yetkilendirmesi
- Müşteri, cihaz ve servis fişi yönetimi
- Otomatik servis fişi ve güvenli takip kodu oluşturma
- Durum geçmişi ve tahmini ücret takibi
- Telefonun son dört hanesiyle takip doğrulaması
- Beş hatalı doğrulamada 10 dakikalık kilit
- SMTP üzerinden gerçek takip e-postası gönderimi
- Web, Windows ve Android istemcileri
- Mobilde güvenli depolama kullanan “Beni hatırla” seçeneği

## Proje Yapısı

- `DeviceService.API`: REST API, JWT, Swagger ve SMTP entegrasyonu
- `DeviceService.Core`: Entity, enum ve arayüzler
- `DeviceService.Data`: EF Core DbContext, migration ve repository katmanı
- `DeviceService.Services`: İş mantığı ve ortak API istemcisi
- `DeviceService.Shared`: Web ve MAUI tarafından kullanılan Razor bileşenleri
- `DeviceService.Web`: Blazor Web uygulaması
- `DeviceService.Maui`: .NET MAUI Blazor Hybrid uygulaması

## Gereksinimler

- .NET 10 SDK
- SQL Server veya SQL Server LocalDB
- Android için .NET MAUI workload ve Android SDK
- İsteğe bağlı bir SMTP hesabı

## Güvenli Yerel Yapılandırma

Gerçek parola, SMTP anahtarı, JWT anahtarı ve yayın URL’si repoya yazılmamalıdır. API projesi `dotnet user-secrets` kullanır.

Önce en az 32 baytlık rastgele bir JWT anahtarı oluştur:

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 48
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $jwtKey --project .\DeviceService.API\DeviceService.API.csproj
$rng.Dispose()
```

Servis hesabı oluşturmayı koruyan kayıt kodunu belirle:

```powershell
dotnet user-secrets set "Registration:ServiceCode" "KENDI-GUCLU-KAYIT-KODUN" --project .\DeviceService.API\DeviceService.API.csproj
```

Bu kod yalnızca servis hesabı kaydında istenir. Müşteri hesabı kaydı için gerekmez.

## SMTP Yapılandırması

SMTP kullanmak istemiyorsan bu bölümü atlayabilirsin; servis fişi kaydolur fakat e-posta gönderimi başarısız olarak raporlanır.

```powershell
dotnet user-secrets set "Email:Host" "smtp.example.com" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:Port" "587" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:UserName" "smtp-kullanici" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:Password" "smtp-parolasi-veya-anahtari" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:FromAddress" "no-reply@example.com" --project .\DeviceService.API\DeviceService.API.csproj
dotnet user-secrets set "Email:PublicAppBaseUrl" "https://web-adresin.example.com" --project .\DeviceService.API\DeviceService.API.csproj
```

Kaydedilmiş değerlerin adlarını kontrol etmek için:

```powershell
dotnet user-secrets list --project .\DeviceService.API\DeviceService.API.csproj
```

Bu komut değerleri de gösterir; çıktısını ekran görüntüsü olarak paylaşma.

## Veritabanı

Varsayılan geliştirme bağlantısı SQL Server LocalDB kullanır. Şemayı oluşturmak için:

```powershell
dotnet ef database update --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
```

Tüm yerel verileri silip boş şemayı yeniden kurmak için:

```powershell
dotnet ef database drop --force --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
dotnet ef database update --project .\DeviceService.Data\DeviceService.Data.csproj --startup-project .\DeviceService.API\DeviceService.API.csproj
```

## Çalıştırma

API:

```powershell
dotnet run --project .\DeviceService.API\DeviceService.API.csproj --launch-profile http
```

Swagger geliştirme ortamında `http://localhost:5113/swagger` adresindedir.

Web:

```powershell
dotnet run --project .\DeviceService.Web\DeviceService.Web.csproj
```

Windows MAUI:

```powershell
dotnet run --project .\DeviceService.Maui\DeviceService.Maui.csproj -f net10.0-windows10.0.19041.0
```

## Android APK

API adresi kaynak kodda tutulmaz. APK oluştururken `DeviceServiceApiBaseUrl` parametresiyle verilir. Değer, sonuna `/` eklenmiş ve istemcinin `api/Health` isteğini doğru endpoint’e ulaştıran API taban adresi olmalıdır.

```powershell
dotnet publish .\DeviceService.Maui\DeviceService.Maui.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=apk -p:DeviceServiceApiBaseUrl=https://api-adresin.example.com/
```

İmzalı APK şu dizinde oluşur:

```text
DeviceService.Maui\bin\Release\net10.0-android\publish\
```

## Güvenlik

- API endpointleri varsayılan olarak kimlik doğrulaması ister.
- Cihaz, müşteri ve servis fişi yönetimi yalnızca `Service` rolüne açıktır.
- Müşteri paneli yalnızca ilgili müşteri JWT’sindeki `customerId` ile veri getirir.
- Giriş ve kayıt endpointleri IP başına dakikada 10 istekle sınırlandırılmıştır.
- Servis hesabı kaydı gizli kayıt kodu gerektirir.
- Parolalar PBKDF2-SHA256, rastgele salt ve 210.000 iterasyonla hashlenir.
- Takip tokenları kriptografik rastgele üretilir.
- Takip ekranı telefonun tamamını döndürmez ve doğrulama denemelerini sınırlar.
- Mobil “Beni hatırla” şifreyi değil, JWT oturumunu işletim sisteminin güvenli deposunda saklar.
- Swagger yalnızca Development ortamında açılır.

## GitHub’a Yükleme

Önce aşağıdaki aramada gerçek parola, anahtar, e-posta veya kişisel tünel URL’si kalmadığını kontrol et:

```powershell
rg -n -i "password|secret|api.?key|smtp|ngrok|tailscale|trycloudflare" . -g "!**/bin/**" -g "!**/obj/**"
```

Daha sonra:

```powershell
git init
git add .
git status
git commit -m "Initial commit"
```

`git status` çıktısında `bin`, `obj`, `.user`, log, sertifika veya anahtar dosyaları görünmemelidir. GitHub’da boş bir depo oluşturduktan sonra GitHub’ın verdiği `remote add` ve `push` komutlarını kullanabilirsin.
## Docker ile Çalıştırma

Docker yapısı dört servisten oluşur:

- `db`: Kalıcı volume kullanan SQL Server Express
- `api`: ASP.NET Core API ve otomatik EF Core migration
- `web`: Blazor Web uygulaması
- `proxy`: Web’i `/`, API’yi `/api/` altında sunan Nginx

Örnek ortam dosyasını kopyala ve gerçek değerleri yalnızca `.env` içine yaz:

```powershell
Copy-Item .env.example .env
```

`.env` Git tarafından dışlanır. `SQL_SA_PASSWORD`, `JWT_KEY` ve `SERVICE_REGISTRATION_CODE` alanları güçlü ve birbirinden farklı olmalıdır. SMTP alanları isteğe bağlıdır.

Container’ları oluşturup başlat:

```powershell
docker compose up -d --build
```

Uygulama varsayılan olarak `http://localhost:8080` adresinde açılır. Durumu ve logları kontrol etmek için:

```powershell
docker compose ps
docker compose logs -f api web proxy
```

Container’ları durdurmak için:

```powershell
docker compose down
```

Veritabanı volume’unu da kalıcı olarak silmek için yalnızca veri kaybını kabul ettiğinde çalıştır:

```powershell
docker compose down -v
```

## Production Deploy

Compose yapısı Docker çalıştırabilen tek bir Linux sunucuya dağıtılabilir. Sunucuda `.env` oluştur, `PUBLIC_BASE_URL` değerini gerçek HTTPS adresine ayarla ve ardından `docker compose up -d --build` çalıştır.

Public ortamda Nginx’in önünde HTTPS sağlayan bir load balancer veya alan adı/sertifika yöneten bir reverse proxy kullanılmalıdır. API ve Web container portları doğrudan internete açılmaz; yalnızca `proxy` servisi yayınlanır.

Android APK’yı production adresine bağlamak için:

```powershell
dotnet publish .\DeviceService.Maui\DeviceService.Maui.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=apk -p:DeviceServiceApiBaseUrl=https://uygulama-adresin.example.com/
```
