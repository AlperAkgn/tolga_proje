# AFTER/FORM — PowerShell ve CLI kullanım rehberi

Bu rehber, projeyi kendi başına çalıştırmak, durumunu görmek, hata incelemek ve geliştirme işlemlerini anlamak içindir. Komut örnekleri normal PowerShell penceresinde kullanılabilir. Her komutun açıklamasından hangi durumda çalıştırılacağını ve neyi değiştirdiğini öğrenebilirsin.

## 1. Terminal, PowerShell, CLI ve betik

| Kavram | Anlamı | Bu projedeki örnek |
|---|---|---|
| Terminal | Komutları yazdığın ve çıktıları gördüğün pencere | Windows Terminal |
| PowerShell | Yazdığın komutları yorumlayan kabuk ve betik dili | `Get-Location`, bir `.ps1` dosyasını çalıştırma |
| CLI | Bir programı komutlarla kullanma arayüzü | `docker ...`, `dotnet ...` |
| Betik / script | Birden fazla işlemi sırayla yapan dosya | `Start-BackendDocker.ps1` |
| Parametre / seçenek | Komuta verdiğin ayar | `-Port 8080`, `--tail 100` |

PowerShell'de `docker ps` yazınca PowerShell, Docker CLI programını başlatır. Docker CLI motorla konuşup konteyner listesini döndürür. Bizim `.ps1` betiklerimiz de gerekli Docker/.NET komutlarını uygun ayarlarla çalıştırır.

`PS C:\Windows\System32>` terminalin bulunduğu klasörü gösteren istemdir. Kopyalarken komutun başına bu kısmı ekleme.

## 2. Her yeni PowerShell penceresinde başlangıç

Önce proje yolunu bir değişkene koy ve proje klasörüne geç:

```powershell
$afRoot = "C:\Users\Windows11\Documents\Codex\2026-09-27\sen"
Set-Location -LiteralPath $afRoot
```

`$afRoot` bu terminal oturumu boyunca proje yolunu tutar. Pencereyi kapatınca değişken gider; yeni pencerede bu iki satırı yeniden yaz. Değişken tanımlamak veritabanını veya proje dosyalarını değiştirmez.

Şimdi nerede olduğunu ve klasörde ne bulunduğunu görebilirsin:

```powershell
Get-Location
Get-ChildItem
```

| Komut | Görevi |
|---|---|
| `Get-Location` | Bulunduğun klasörü gösterir; kısa adı `pwd` |
| `Set-Location` | Klasör değiştirir; kısa adı `cd` |
| `Get-ChildItem` | Dosyaları ve klasörleri listeler; kısa adı `dir` |
| `Clear-Host` | Ekrandaki çıktıyı temizler; kısa adı `cls` |
| `Get-Command docker` | Docker komutunun hangi programdan geldiğini gösterir |

`Clear-Host` yalnızca görüntüyü temizler; dosya veya kayıt silmez. Klasör değiştirme ayrıntıları: [Microsoft Set-Location rehberi](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/set-location?view=powershell-5.1).

## 3. Bir komutu parçalara ayırarak oku

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Logs
```

| Parça | Anlamı |
|---|---|
| `&` | Tırnak içindeki yolu bir komut/betik olarak çalıştır |
| `"$afRoot\scripts\Manage-BackendDocker.ps1"` | Çalıştırılacak dosyanın yolu |
| `-Action` | Betiğin parametresi |
| `Logs` | Parametreye verdiğimiz değer |

Tırnak, özellikle boşluk içeren yolları ve isimleri tek parça tutar. Parametreler yolun kapanan tırnağının **dışında** yazılır. Örneğin `-Port 8081`, portu 8081 seçer. `#` ile başlayan kısım PowerShell'de açıklamadır. `|` ise soldaki çıktıyı sağdaki komuta aktarır. [Microsoft PowerShell operatörleri](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_operators?view=powershell-5.1).

Bir başka örnek:

```powershell
docker logs --tail 100 afterform-backend-1
```

Burada `docker` program, `logs` işlem, `--tail 100` son 100 satır seçeneği, `afterform-backend-1` hedef konteynerdir.

## 4. Günlük kullanım için temel komutlar

Docker Desktop açık olmalı. Normal site adresimiz **http://localhost:8080/**; mevcut MySQL konteynerimiz **688c9576f123**.

### 4.1. Projeyi başlat veya kod değişikliğini uygula

```powershell
& "$afRoot\scripts\Start-BackendDocker.ps1" -ContainerName 688c9576f123
```

Mevcut MySQL'i ve kalıcı depolamasını kontrol eder, ağ bağlantısını hazırlar, uygulama ayarlarını üretir, imajı güncel kaynaklardan oluşturur ve backend'i arka planda başlatır. MySQL durmuşsa mevcut konteyneri başlatır. Güncel imajdan backend konteynerini yeniden oluşturduğu için kaynak kod değişiklikleri de etkinleşir.

Başarılı son satır:

```text
Tamamlandi: http://localhost:8080/ / MySQL connected / schema upToDate.
```

İlk derleme internetten imaj/paket indirebilir. Komut bittikten sonra terminali kapatabilirsin; Docker Desktop ve MySQL çalışmaya devam etmeli. Site zaten çalışıyorsa yalnızca tarayıcıdan açman yeterli.

Portu değiştirmek için:

```powershell
& "$afRoot\scripts\Start-BackendDocker.ps1" -ContainerName 688c9576f123 -Port 8081
```

Bu durumda site http://localhost:8081/ olur; yönetim betiği kaydedilen portu kullanır. Sonradan varsayılan 8080'e dönmek için ilk komutu tekrar kullan.

### 4.2. Durumu gör

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Status
```

`afterform` Compose projesinin konteynerlerini gösterir. Mevcut MySQL bu Compose projesinin dışında olduğu için bu listede görünmeyebilir. Backend için `healthy` beklenir; bizim sağlık kontrolümüz MySQL bağlantısını ve migration durumunu da kapsar.

### 4.3. Logları oku

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Logs
```

Backend'in son 100 log satırını getirir. Site açılmadığında veya API hata verdiğinde ilk bakacağın yerlerden biridir.

### 4.4. Backend'i yeniden başlat

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Restart
```

Mevcut backend konteynerini durdurup yeniden başlatır. Yeni kaynak kodu derlemez. Kaynak dosyalarını değiştirdiğinde 4.1'deki başlatma/derleme komutunu kullan. [Docker restart açıklaması](https://docs.docker.com/reference/cli/docker/compose/restart/).

### 4.5. Backend'i durdur

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Stop
```

Backend'i durdurur; MySQL kendi konteynerinde çalışmaya devam eder. Kalıcı veri klasörleri yerlerinde kalır. [Docker stop açıklaması](https://docs.docker.com/reference/cli/docker/compose/stop/).

### 4.6. Aynı imajdan backend'i yeniden oluştur

```powershell
& "$afRoot\scripts\Manage-BackendDocker.ps1" -Action Recreate
```

Mevcut imajdan yeni backend konteyneri oluşturur ve sağlıklı duruma gelmesini bekler. Kaynak kodu yeniden derlemez. Demo ve anahtar klasörleri aynı yerlere bağlanır. Docker konteynerinin yeniden oluşturulmasını gözlemlemek için kullanılabilir.

### Üç işlemin farkı

| İşlem | Kaynak koddan yeni imaj oluşturur mu? | Backend konteynerini yeniler mi? |
|---|---|---|
| `Restart` | Hayır | Aynı konteyneri yeniden başlatır |
| `Recreate` | Hayır | Mevcut imajdan yeniden oluşturur |
| `Start-BackendDocker.ps1` | Evet, uygun derleme önbelleğini kullanır | Güncel imajdan yeniden oluşturur |

## 5. Docker CLI'yi doğrudan kullanmak

### Çalışan veya durmuş bütün konteynerleri gör

```powershell
docker ps
docker ps -a
```

İlk komut çalışanları, `-a` ekleneni durmuş olanlar dahil tüm konteynerleri gösterir. `NAMES`, `STATUS` ve `PORTS` sütunlarına bak. [Docker konteyner listeleme rehberi](https://docs.docker.com/reference/cli/docker/container/ls/).

### MySQL'in çalışmasını yönet

```powershell
docker start 688c9576f123
```

Mevcut MySQL konteynerini başlatır. Bu projede başlatma betiği gerektiğinde bunu zaten yapar.

```powershell
docker stop 688c9576f123
```

Bu komut **MySQL sunucusunun tamamını** durdurur. Bu MySQL'e bağlı başka projeler de bağlantı kuramaz. Site dosyaları backend'den sunulmaya devam edebilse de veritabanı isteyen giriş/demo işlemleri çalışmaz. MySQL'i durdurmak veri volume'unu silmez.

### Canlı backend loglarını takip et

```powershell
docker logs --tail 100 --follow afterform-backend-1
```

Yeni loglar geldikçe ekranda görünür. Burada `Ctrl+C` yalnızca log takibini bitirir; arka plandaki Docker backend'ini durdurmaz. [Docker logs rehberi](https://docs.docker.com/reference/cli/docker/container/logs/).

### Sağlık ve depolama bilgisini seçerek oku

```powershell
docker inspect --format '{{.State.Health.Status}}' afterform-backend-1
docker inspect --format '{{json .Mounts}}' 688c9576f123
docker volume ls
docker volume inspect mysql_data
```

İlk satır backend'in sağlık durumunu, ikinci satır MySQL'in mount'larını, sonrakiler volume listesini ve `mysql_data` bilgisini gösterir. Şablonlar tek tırnak içinde yazılır. Tam MySQL `docker inspect` çıktısı ortam değişkenlerinde root parolası içerebilir; burada yalnızca gereken alanı seçiyoruz.

### Konteynerin içinde bir kabuk aç

```powershell
docker exec -it afterform-backend-1 sh
```

Çalışan konteynerde Linux kabuğu açılır. Artık o kabukta şu komutlar kullanılır:

```sh
pwd
ls
whoami
exit
```

`exit` ile PowerShell'e dönersin; backend'in ana süreci çalışmaya devam eder. Bu inceleme için uygundur. Kalıcı kod değişiklikleri Windows'taki kaynak dosyalarda yapılıp imaj yeniden oluşturulmalıdır. [Docker exec rehberi](https://docs.docker.com/reference/cli/docker/container/exec/).

### Compose komutunun betiğin içindeki karşılığı

Projeye uygun ayarlarla yalnızca durum okumak için:

```powershell
docker --config "$afRoot\.local\docker-cli" compose --project-name afterform --project-directory "$afRoot" --env-file "$afRoot\.local\docker\compose.env" --file "$afRoot\compose.yaml" ps --all
```

| Seçenek | Anlamı |
|---|---|
| `--config` | Docker CLI'nin yerel ayar klasörü; uygulama bağlantı sırrı değildir |
| `compose` | Compose proje yönetimi |
| `--project-name afterform` | Yönetilecek proje grubu |
| `--project-directory` | Proje klasörü |
| `--env-file` | Kaydedilmiş host portu gibi Compose değişkenleri |
| `--file` | Kullanılacak Compose dosyası |
| `ps --all` | Proje konteynerlerini listele |

Başlatma betiğinin sonundaki `up --build --detach --force-recreate --wait --wait-timeout 120 backend` kısmını şöyle okuyabilirsin:

- `up`: Servisi oluştur ve başlat.
- `--build`: İmajı oluştur.
- `--detach`: Arka planda çalıştır; kısa biçimi `-d`.
- `--force-recreate`: Konteyneri yeniden oluştur.
- `--wait`: Servisin hazır/sağlıklı olmasını bekle.
- `--wait-timeout 120`: Bu hazır olma bekleyişinin üst sınırı 120 saniye; imaj derleme süresinin tamamı değildir.
- `backend`: Compose içindeki servis adı; oluşan konteynerin adı `afterform-backend-1`.

[Docker Compose up rehberi](https://docs.docker.com/reference/cli/docker/compose/up/) bu seçenekleri açıklar. Günlük kullanımda betik, ağ ve depolama hazırlığını da yaptığı için proje komutunu bir arada sunar.

## 6. PowerShell'den API'ye bak

```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:8080/api/health/live"
Invoke-RestMethod -Method Get -Uri "http://localhost:8080/api/health/ready"
```

İlk istek uygulamanın yanıt verdiğini, ikinci istek gerçek MySQL bağlantısını ve migration durumunu gösterir. İkinci yanıtın alanları `status=ready`, `database=connected`, `schema=upToDate` olmalıdır. Portu değiştirdiysen URL'yi de değiştir. JSON yanıtını PowerShell nesnesine çeviren komutun açıklaması [Invoke-RestMethod rehberinde](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/invoke-restmethod?view=powershell-5.1) bulunur.

İstersen ham HTTP yanıtını görebilirsin:

```powershell
curl.exe -i "http://localhost:8080/api/health/ready"
```

Windows PowerShell'de `curl` bir PowerShell alias'ı olabileceği için burada özellikle `curl.exe` kullanıyoruz.

Tarayıcıda açık sanatçı oturumu, yeni PowerShell veya Postman isteğine otomatik taşınmaz. Oturum çerezi olmadan `/api/demos` isteğinin 401 dönmesi beklenir.

| HTTP kodu | Bu projede anlamı |
|---|---|
| `200` | Başarılı okuma / giriş / sağlık yanıtı |
| `201` | Demo kaydedildi |
| `204` | Çıkış başarılı; yanıt gövdesi yok |
| `400` | Alan, dosya, CSRF veya multipart doğrulama hatası |
| `401` | Geçerli oturum yok veya giriş başarısız |
| `404` | Kayıt yok ya da oturumdaki sanatçıya ait değil |
| `413` | Gövde/dosya boyutu sınırı; bazı multipart boyut hataları açık mesajla 400 olabilir |
| `429` | İstek sayısı sınırı |
| `503` | Veritabanı, şema hazırlığı veya depolama kullanılamıyor |

## 7. Yeni sanatçı hesabı oluştur

```powershell
& "$afRoot\scripts\Create-Artist.ps1" -UserName "artist.egitim" -DisplayName "Eğitim Sanatçısı"
```

Bu işlem veritabanına **yeni hesap ekler**. `-UserName` giriş adı, `-DisplayName` portalda görünen isimdir. Parolayı terminalde iki kez sorar; yazarken karakter görünmemesi normaldir. Parola 15–128 karakter olmalı. Aynı kullanıcı adı zaten varsa betik hata verir; var olan hesabın parolasını değiştirmez.

Araç Windows'taki .NET SDK ile çalışır ve mevcut MySQL'e bağlanır. Backend Docker'da aynı veritabanını kullandığından yeni hesapla http://localhost:8080/artist/login/ adresinden giriş yapabilirsin. Backend'i sırf hesap ekledin diye yeniden başlatmak gerekmez. Eski Windows backend'i Debug dosyalarını kullanarak çalışıyorsa hesabı oluşturmadan önce o terminalde Ctrl+C ile durdur.

## 8. Veritabanı kurulumu ve migration

**Migration**, tablo yapısındaki sürümlü değişikliktir. Örneğin yeni bir alan eklemek şema değişikliği; yeni sanatçı hesabı veya demo eklemek ise veri değişikliğidir.

Mevcut migration'ları MySQL'e uygulamak için:

```powershell
& "$afRoot\scripts\Update-Database.ps1"
```

Bu komut EF aracını hazırlar, backend'i derler ve bekleyen migration'ları uygular. Şu anda şema `upToDate` olduğu için günlük site açılışında çalıştırman gerekmiyor. Şema değişikliği uygulamadan önce uygun veritabanı/dosya yedeği al; migration veri üzerinde değişiklik yapabilir. [Microsoft migration uygulama rehberi](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

Yeni model değişikliği geliştirdiğinde EF CLI işlemleri şöyledir. Önce proje klasöründe ol ve proje yolunu tanımla:

```powershell
$afProject = Join-Path $afRoot "backend\Afterform.Api\Afterform.Api.csproj"
dotnet tool restore --configfile "$afRoot\NuGet.config"
dotnet ef migrations list --project "$afProject" --startup-project "$afProject"
```

`tool restore` projedeki manifest'te kayıtlı `dotnet-ef` aracını hazırlar. `migrations list` migration listesini okur; modelden yeni değişiklik oluşturmaz.

**Yalnızca gerçek bir model değişikliği geliştirdiğinde**, uygun bir migration adı seçerek:

```powershell
dotnet ef migrations add ArtistProfileUpdate --project "$afProject" --startup-project "$afProject"
```

Bu örnek komut migration kaynak dosyası üretir; kendi başına MySQL'e uygulamaz. Üretilen değişiklikler incelendikten sonra `Update-Database.ps1` ile uygulanır. `ArtistProfileUpdate` örnek isimdir; şu an yeni model değişikliği geliştirmedik. [Microsoft EF CLI rehberi](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

İlk veritabanı hazırlama aracı:

```powershell
& "$afRoot\scripts\Initialize-Database.ps1" -ContainerName 688c9576f123
```

İlk kurulumda proje veritabanını ve uygulama/migration kullanıcılarını hazırlar, yerel bağlantıyı kaydeder. Biz bunu zaten yaptık. Yeni bilgisayar veya yeni bağlantı kurulumu için kullanılır; günlük başlatma adımı değildir.

## 9. SQL nerede yazılır?

SQL, MySQL'e gönderdiğin sorgu dilidir. PowerShell'e doğrudan `SELECT ...` yazmak SQL sorgusu çalıştırmaz.

### Workbench ile

Mevcut bağlantını aç; SQL sekmesinde şu okuma sorgularını kullanabilirsin:

```sql
USE afterform;
SHOW TABLES;

SELECT Id, UserName, DisplayName
FROM AspNetUsers
LIMIT 20;

SELECT Id, Title, Genre, Status, SubmittedAtUtc
FROM Demos
ORDER BY Id DESC
LIMIT 20;
```

`USE` şemayı seçer; `SHOW TABLES` tabloları listeler; `SELECT` veri okur; `ORDER BY` sıralar; `LIMIT` dönen satır sayısını sınırlar. Bu örnekler veri değiştirmez. Workbench'teki yönetici erişimi, sitedeki sanatçının kendi kayıtlarını görme yetkisinden farklıdır. [MySQL SELECT rehberi](https://dev.mysql.com/doc/refman/8.0/en/select.html).

### MySQL CLI ile — isteğe bağlı

Konteynerin MySQL **root parolasını biliyorsan**:

```powershell
docker exec -it 688c9576f123 mysql -u root -p
```

Parolayı etkileşimli sorar. Bu, sanatçı giriş parolan değildir. `-p` yanına parola yazma; terminal geçmişine girmesin. Root parolasını bilmiyorsan mevcut Workbench bağlantını kullanabilirsin.

İstem `mysql>` olunca SQL yazarsın:

```sql
USE afterform;
SHOW TABLES;
SELECT COUNT(*) AS DemoCount FROM Demos;
exit;
```

`exit;` ile PowerShell'e dönersin. SQL sorguları noktalı virgülle tamamlanır. [MySQL komut satırı istemcisi](https://dev.mysql.com/doc/refman/8.0/en/mysql.html).

## 10. .NET CLI komutları

Mevcut SDK ve sürümünü görmek için:

```powershell
dotnet --version
dotnet --info
```

Proje yolunu tanımla:

```powershell
$afProject = Join-Path $afRoot "backend\Afterform.Api\Afterform.Api.csproj"
```

### Bağımlılıkları hazırla

```powershell
dotnet restore "$afProject" --locked-mode --configfile "$afRoot\NuGet.config"
```

NuGet paketlerini, mevcut paket kilidine uygun olarak indirir/hazırlar. `--locked-mode` kilit dosyasıyla uyumsuz bağımlılık değişikliğini hata olarak bildirir. [dotnet restore rehberi](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore).

### Derle

```powershell
dotnet build "$afProject" --no-restore --configuration Release
```

C# kaynaklarını derler. Sunucuyu başlatmaz; Docker imajını güncellemez. `--no-restore` önceki restore sonucunu kullanır. Derleme varsayılan olarak restore yapabildiği için bu seçenek hazırlığın zaten tamamlandığını belirtir. [dotnet build rehberi](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build).

### Yayına uygun çıktı üret

```powershell
dotnet publish "$afProject" --no-restore --configuration Release --output "$afRoot\.local\manual-publish" /p:UseAppHost=false
```

Uygulama ve gerekli dosyalarını seçilen klasöre toplar. Bu komut kendi başına internete yayın yapmaz. `--output` çıktı klasörü, `Release` derleme yapılandırmasıdır. Dockerfile da publish işlemini imaj oluştururken kullanır. [dotnet publish rehberi](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish).

### Windows'ta backend çalıştır — ayrı geliştirme seçeneği

```powershell
& "$afRoot\scripts\Start-Backend.ps1"
```

Windows'ta .NET backend'i http://localhost:5000/ üzerinde çalıştırır. `Now listening on` görünür ve terminal açık kalmalıdır. `Ctrl+C` bu Windows sunucusunu durdurur. İçeride `dotnet run` kullanır. [dotnet run rehberi](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-run).

Normal Docker kullanımında adresimiz 8080'dir. İki localhost sürümündeki oturum çerezleri portlara göre ayrılmadığından aynı anda iki sürümde giriş yaparsan oturumlar etkilenebilir.

## 11. Postman, Newman, Node ve npm

| Araç | Projedeki görevi |
|---|---|
| Postman | API isteklerini arayüzden gönderip yanıtları inceleme |
| Newman | Postman koleksiyonunu programatik/terminal üzerinden çalıştırma |
| Node.js | Newman çalıştırıcısının JavaScript kodunu yürütme |
| npm | Bu çalıştırıcının paketlerini kurma |

Sürümlerini görmek için:

```powershell
node --version
npm.cmd --version
```

Gerektiğinde test aracının paketlerini kilit dosyasına göre yeniden kurmak için:

```powershell
npm.cmd ci --prefix "$afRoot\tests\postman" --cache "$afRoot\.local\npm-cache" --ignore-scripts --no-audit --no-fund
```

Bu işlem Newman bağımlılıklarını kurar. Site başlatma komutumuz Docker betiğidir. PowerShell'de `npm.cmd` yazmak, Windows'taki npm komut dosyasını açıkça seçer.

Güvenlik senaryolarını **yeniden çalıştırmak istediğinde**:

```powershell
& "$afRoot\scripts\Test-ArtistSecurity.ps1"
```

Bu komut yalnızca sonuç okumaz: iki yeni QA hesabı oluşturur, yapay WAV yükler, hatalı istekler gönderir ve QA B hesabının kilitlenmesini dener. QA kayıtları inceleme için veritabanında tutulur. Günlük sanatçı parolanı kullanmaz. Test sonunda ayrı test sunucusunu kapatır ve raporu günceller.

**Bu betik mevcut Docker backend'ine test göndermiyor.** Windows'ta 5019 portunda geçici .NET sunucusu açıp aynı MySQL'e bağlanıyor. Dolayısıyla bu koşu tek başına Linux konteynerindeki bütün davranışları doğrulamaz. [Newman rehberi](https://learning.postman.com/docs/reference/newman-cli/installing-running-newman/).

Postman arayüzünde günlük Docker API'sine istek gönderirken `baseUrl=http://localhost:8080` kullan. Koleksiyonun kendi varsayılanı eski 5000 veya test 5019 olabilir; seçtiğin environment/collection değişkenini kontrol et. Giriş akışında CSRF al, login yap, giriş sonrası yeni CSRF al; cookie jar oturumu taşır.

## 12. Dosyanın SHA-256 özetini oku

Kendi dosyanın gerçek yolunu ver:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath "C:\kendi\dosyan\demo.wav"
```

Bu komut dosyayı değiştirmez; özetini hesaplar. MySQL'deki demo `FileSha256` alanıyla karşılaştırılabilir. Bu özet, ses dosyasının bayt bütünlüğü içindir; sanatçı parolasının Identity hash'i farklı bir işlemdir. [Microsoft Get-FileHash rehberi](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/get-filehash?view=powershell-5.1).

## 13. Yardım almayı ve hata okumayı öğren

```powershell
Get-Help Get-Location
Get-Help Set-Location -Examples
Get-Command docker
docker --help
docker logs --help
dotnet --help
```

Programların `--help` seçeneği alt komutları/seçenekleri gösterir. PowerShell cmdlet'lerinde `Get-Help` kullanılır. [Microsoft Get-Help rehberi](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/get-help?view=powershell-5.1).

| Durum | İlk kontrol |
|---|---|
| `not recognized` / komut bulunamadı | Yazım, tam yol, `Get-Command`, tırnakların dışındaki parametreler |
| Dosya bulunamadı | `Get-Location`, doğru proje yolu ve `$afRoot` değişkeni |
| `Docker engine` bağlantı hatası | Docker Desktop açık mı, Linux containers modu mu? |
| `port is already allocated` | 8080 dolu mu? Başlatmada `-Port 8081` seç |
| `unhealthy` / `503` | Backend logları, MySQL'in çalışması, readiness ve migration durumu |
| API'de `401` | İstek kendi cookie oturumuna sahip mi? |
| API'de `400` | Yanıttaki doğrulama mesajı; login/upload ise CSRF de kontrol edilir |
| API'de `429` | İstek sınırı dolmuştur; `Retry-After` süresini bekle |

Betik çalıştırma politikası hatası gelirse önce:

```powershell
Get-ExecutionPolicy -List
```

Yalnızca bu hata varsa ve bilgisayarın yönetim politikası izin veriyorsa, mevcut PowerShell penceresi için:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
```

Bu ayar pencere kapanınca sona erer; şirket/okul Group Policy ayarını geçersiz kılmaz. Mevcut betikler zaten çalışıyorsa değiştirmen gerekmez. [Microsoft execution policy rehberi](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies?view=powershell-5.1).

Hata incelemek için çalıştırdığın komutu, bulunduğun klasörü ve son ilgili hata satırlarını paylaş. Parola, oturum token'ı ve bağlantı sır dosyasını paylaşma. Kırmızı çıktı tek başına teşhis değildir; `port`, `authentication`, `migration`, `not found` gibi hata nedenini belirten satır önemlidir.

## 14. Projedeki betiklerin tamamının görev özeti

| Betik | Ne zaman kullanılır? |
|---|---|
| [Start-BackendDocker.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Start-BackendDocker.ps1) | Docker backend'ini başlatmak veya yeni kaynak kodu uygulamak |
| [Manage-BackendDocker.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Manage-BackendDocker.ps1) | Status, Logs, Stop, Restart, Recreate |
| [Initialize-DockerConfig.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Initialize-DockerConfig.ps1) | Docker sır dosyası, port ayarı ve kalıcı klasörleri hazırlamak; başlatma betiği çağırır |
| [Docker-Common.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Docker-Common.ps1) | Docker betiklerinin ortak yardımcıları; ayrı bir başlatma işlemi değildir |
| [Start-Backend.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Start-Backend.ps1) | Windows'ta 5000 portunda .NET çalıştırmak |
| [Initialize-Database.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Initialize-Database.ps1) | İlk MySQL/proje kullanıcıları/yerel bağlantı kurulumu |
| [Update-Database.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Update-Database.ps1) | Bekleyen EF migration'larını MySQL'e uygulamak |
| [Complete-BackendSetup.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Complete-BackendSetup.ps1) | Migration uygulayıp Windows sunucusunu açmak; ilk kurulum akışı |
| [Create-Artist.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Create-Artist.ps1) | Yeni sanatçı hesabı oluşturmak |
| [Enable-ArtistAccess.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Enable-ArtistAccess.ps1) | Yeni hesap oluşturup Windows sunucusunu açmak; ilk sanatçı erişimi akışı |
| [Test-ArtistSecurity.ps1](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/scripts/Test-ArtistSecurity.ps1) | Yeni QA kayıtlarıyla Postman/Newman senaryolarını yeniden yürütmek |

İki eski birleşik kurulum betiği (`Complete-BackendSetup`, `Enable-ArtistAccess`) Windows sunucusunu açar. Günlük Docker kullanımında başlatma betiğin `Start-BackendDocker` olmalıdır.

## 15. Durdurmak, kaldırmak ve veri silmek

`stop` çalışan servisi durdurur. `rm` konteyneri kaldırır. `compose down` ilgili Compose projesinin konteynerlerini ve yönettiği ağları kaldırır. `down -v` bu projede tanımlı uygun volume'ları da kaldırma seçeneğidir. Bizim Compose dosyamızda MySQL servisi ve `mysql_data` volume'u tanımlı değildir; MySQL başka bir mevcut konteynerdir. Bu nedenle bu projenin `down` komutunu bütün bilgisayardaki MySQL'i yöneten bir komut gibi düşünme. [Docker Compose down rehberi](https://docs.docker.com/reference/cli/docker/compose/down/).

Veri volume'unu veya Windows'taki demo klasörünü silmek ayrı işlemdir. `volume rm`, volume içeren `prune` seçenekleri, SQL `DROP/TRUNCATE/DELETE` ve PowerShell `Remove-Item` gibi işlemler veri silebilir. Günlük başlatma/durdurma için rehberdeki `Start` ve `Stop` akışı yeterlidir.

## 16. İlk öğrenme sırası

1. Proje klasörüne geç; konum ve dosya listesini oku.
2. `docker ps -a` ile backend ve MySQL'i bul.
3. `Status` ile proje durumunu, `Logs` ile backend çıktısını oku.
4. `Invoke-RestMethod` ile readiness yanıtını gör.
5. Workbench'te `SHOW TABLES` ve sınırlı `SELECT` çalıştır.
6. Ardından kontrollü olarak Stop/Start akışını öğren.
7. Hesap oluşturma, migration ve QA testlerini ihtiyaç olduğunda uygula.

Bir komutu okurken dört şeyi belirle: **hangi programı çalıştırıyor, hangi hedefte işlem yapıyor, hangi seçenekleri alıyor ve veri/kod/çalışma durumundan hangisini değiştiriyor?**
