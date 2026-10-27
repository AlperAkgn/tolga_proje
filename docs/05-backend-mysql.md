**5. adım: ASP.NET Core, EF Core ve MySQL temeli**

Bu adımda AFTER/FORM'un sunucu uygulaması ve sanatçı–demo veritabanı şeması hazırlandı. Öğrenme hedefi, bir HTTP isteğinin C# koduna nasıl ulaştığını ve C# modellerinin MySQL tablolarına nasıl dönüştüğünü anlamak.

**Mevcut doğrulama durumu**

- .NET projesi derlendi: 0 hata, 0 uyarı.
- İlk migration üretildi; EF, model ile migration arasında fark olmadığını doğruladı.
- Migration'ın gerçek MySQL SQL çıktısı database/InitialArtistPortal.sql dosyasına üretildi.
- Geçici bir .NET sunucusunda ana sayfa, giriş, portal ve gönderim sayfası 200 yanıtıyla açıldı.
- API çalışırlık kontrolü 200; bilinçli olarak ulaşılamayan veritabanı kontrolü 503; tanımsız API yolu yapılandırılmış 404 döndürdü.
- Kullanıcının seçtiği `688c9576f123` konteynerinde MySQL `8.0.46` ile `afterform` veritabanı ve iki proje kullanıcısı hazırlandı. Bağlantı portu `127.0.0.1:3306`.
- `20260930225616_InitialArtistPortal` migration'ı gerçek MySQL veritabanına uygulandı. .NET sunucusu normal PowerShell'den `http://localhost:5000/` adresinde başlatıldı.
- Gerçek sunucunun `/api/health/ready` yanıtı: `status=ready`, `database=connected`, `schema=upToDate`. `/api/health/schema` yanıtında uygulanan ilk migration ve sekiz model tablosu görüldü. Migration geçmişi tablosuyla birlikte kurulum şeması dokuz tablodan oluşur.
- 5. adım tamamlandı. Sanatçı hesabı oluşturma, parola hash'leme ve oturum 6. adımda; gerçek dosya yükleme 7. adımda uygulanacak.

1. **İki farklı uygulama katmanı**

   outputs klasöründeki HTML, CSS ve JavaScript tarayıcıda görünen arayüzdür. backend/Afterform.Api ise C# ile yazılan sunucu uygulamasıdır. ASP.NET Core, gelen HTTP isteğini alır ve uygun sayfayı veya API yanıtını döndürür.

   Geliştirmede .NET doğrudan mevcut outputs dosyalarını sunar. Yayınlama sırasında proje dosyası bu dosyaları wwwroot içine kopyalar. Böylece arayüzün bir kopyasını elle yönetmek gerekmez.

   Varsayılan .NET adresi http://localhost:5000/ olur. /artist/login/ gibi yollar HTML döndürür; /api/health/live gibi yollar JSON döndürür. 8000 portundaki önceki statik önizleme, ayrı bir sunucudur.

   Mevcut formlar hâlâ ön yüz önizlemesidir. Veritabanı tablosunun hazırlanması, formun kendiliğinden gerçek giriş veya yükleme yapması anlamına gelmez.

2. **Program.cs: uygulamanın başlangıç noktası**

   WebApplication.CreateBuilder(args), uygulama ayarlarını, loglamayı ve servis kayıtlarını hazırlar. builder.Services.AddDbContext, veritabanı ile konuşacak AfterformDbContext nesnesini kaydeder. AddControllers ise controller sınıflarını HTTP istekleri için kullanıma açar.

   builder.Build() sonrasında istek hattı oluşturulur. UseExceptionHandler beklenmeyen hataları ele alır; UseStatusCodePages boş hata yanıtlarına JSON gövdesi ekler; UseDefaultFiles ve UseStaticFiles HTML dosyalarını sunar; MapControllers API yollarını eşler. app.Run() sunucuyu çalıştırır.

   Bu işleyişte “middleware”, isteği işleyen hattın bir parçasıdır. Sıra önemlidir: hata yakalama, sonradan çalışan işlemlerdeki hataları da yakalayabilmek için hattın başında bulunur.

3. **EF Core'un görevi**

   Entity Framework Core, C# nesneleri ile ilişkisel veritabanı arasında çalışır. Bir sınıfı tabloya, özelliklerini sütunlara ve nesneler arasındaki ilişkiyi foreign key'e dönüştürür.

   MySql.EntityFrameworkCore, EF'nin MySQL'e uygun SQL üretmesini ve MySQL sürücüsünü kullanmasını sağlar. Kurulu .NET 10 ile EF/Identity 10.0.12 ve Oracle MySQL sağlayıcısı 10.0.9 kullanıldı. Sağlayıcının net10.0 paketi EF Core >= 10.0.9 bağımlılığı bildiriyor; projenin 10.0.12 sürümü bu gereksinimi karşılıyor.

   AfterformDbContext, hangi modellerin veritabanına ait olduğunu ve bağlantının nasıl kullanılacağını bilir. Demos isimli DbSet, demo tablosunu sorgulamak veya değiştirmek için kullanılan giriş noktasıdır; kayıtları kalıcı olarak RAM'de tutan bir liste değildir.

   AddDbContext varsayılan olarak istek başına bir context sağlar. Dependency injection, context nesnesini gerektiğinde controller'a verir; controller kendi bağlantı parolasını okumak veya context'i elle oluşturmak zorunda kalmaz.

4. **Sanatçı hesabı modeli**

   ArtistUser sınıfı, ASP.NET Core Identity'nin IdentityUser sınıfından türetilir. Identity; Id, UserName, NormalizedUserName, PasswordHash ve güvenli hesap yönetimi için kullanılacak diğer alanları sağlar. Projeye DisplayName ve CreatedAtUtc ekledik.

   Hesap tablosunun adı AspNetUsers olacaktır. “AspNet” öneki, Identity'nin varsayılan tablo adlandırmasıdır. Buradaki satırlar uygulama sanatçı hesaplarını temsil edecek.

   PasswordHash sütununun hazırlanmış olması henüz parola hash'lediğimiz anlamına gelmez. Sanatçı oluşturma, parola doğrulama, salt/hash işlemleri ve oturum 6. adımda Identity servisleri ile kurulacak. Bu adımda gerçek sanatçı hesapları veya giriş API'si üretilmedi.

5. **Demo modeli ve sahiplik ilişkisi**

   DemoSubmission sınıfı Demos tablosuna dönüşür. Id kayıt anahtarıdır; ArtistId demoyu gönderen sanatçıyı gösterir; Title, Genre ve Description parçanın bilgilerini tutar.

   İlişki şöyledir:

   AspNetUsers.Id (bir sanatçı) → Demos.ArtistId (o sanatçının birçok demosu)

   Foreign key sayesinde veritabanında bulunmayan bir sanatçıya ait demo kaydı oluşamaz. DeleteBehavior.Restrict, demosu olan bir sanatçının hesabı silindiğinde demoların otomatik silinmesini engeller.

   Bu ilişki tek başına erişim yetkisi sağlamaz. Sanatçının yalnızca kendi demolarını görmesi için ileride backend, oturumdaki kullanıcı kimliğini kullanarak sorguyu sınırlandıracak. Formdan gönderilen bir ArtistId'ye güvenilmeyecek.

   ArtistId ve SubmittedAtUtc üzerinde birlikte index bulunur. Bu, “bu sanatçının demolarını tarihe göre getir” sorgusuna yardımcı olur. Index performans içindir; yetkilendirme değildir.

6. **Durum ve dosya bilgileri**

   DemoStatus bir enum'dur: Submitted, InReview, Accepted, Rejected. EF dönüşümü sayesinde MySQL sütununda sayı yerine bu adlar saklanır. Varsayılan yeni demo durumu Submitted olarak hem modelde hem şema ayarında belirlenir.

   StorageKey, ses dosyasının depodaki özel kimliğini tutar. OriginalFileName, ContentType ve FileSizeBytes ise dosyanın bilgilerini tutar. Ses dosyasının kendisi MySQL sütununa konmaz.

   Bu tasarım, yerel dosya deposundan Azure Blob Storage'a geçmeye uygundur. Gerçek dosya yükleme ve korumalı erişim 7. adımda kurulacak.

   Tarihler UTC olarak tutulacak ve datetime(6) sütunlarına yazılacak. UTC, farklı saat dilimlerinden gelen kayıtların aynı zaman referansını kullanmasını sağlar.

7. **Migration: C# modelinden MySQL şemasına**

   Migration, şema değişikliklerinin sürümlü kaydıdır. InitialArtistPortal migration'ının Up metodu tabloları, index'leri ve ilişkileri oluşturur. Down metodu bu şema değişikliğinin tersini tarif eder.

   ModelSnapshot dosyası, EF'nin sonraki model değişikliklerini karşılaştıracağı şema görünümünü saklar. Yeni bir özellik eklendiğinde snapshot elle değiştirilmez; yeni migration üretilir.

   Migration üretmek ve SQL dosyası üretmek, çalışan bir MySQL sunucusu gerektirmez. Bu projede EF komutlarının bağlantı ayarı yine de hazırlanmış olmalıdır. Migration'ı uygulamak ayrıca gerçek sunucu ve yetkili bağlantı gerektirir. DesignTimeDbContextFactory ayar dosyasını mutlak yolla yükler ve `AfterformMigrations` bağlantısını kullanır. Ayar bulunamazsa açıklayıcı bir hata ile durur; yer tutucu kullanıcıyla bağlanmayı denemez.

   Başarılı uygulamadan sonra Workbench'te yedi Identity tablosu, Demos ve __EFMigrationsHistory görünür. History tablosu hangi migration'ların uygulandığını saklar. EF aynı migration'ı tekrar tekrar uygulamaz.

   İlk SQL çıktısı öğrenme amacıyla database/InitialArtistPortal.sql dosyasındadır. Normal uygulama yolu Update-Database.ps1 / dotnet ef database update olur. SQL çıktısını ayrıca uygulamak gerekli değildir.

8. **Docker MySQL ve iki veritabanı kullanıcısı**

   Initialize-Database.ps1 mevcut MySQL konteynerini bulur; gerekirse başlatır ve yayımlanmış 3306 konteyner portunun bilgisayardaki karşılığını okur. Dolu bir veritabanına bu ilk kurulum şemasını uygulamayı reddeder.

   Oluşturulan afterform_app kullanıcısı yalnızca afterform veritabanında SELECT, INSERT, UPDATE, DELETE işlemleri yapabilir. afterform_migrator kullanıcısı aynı veritabanında tablo yapısını değiştirebilmek için ek yetkilere sahiptir. Önceden aynı adlı bir kullanıcı varsa onun parolası değiştirilmez; yeni kullanıcı için farklı bir ad üretilir.

   Bu kullanıcılar sanatçı hesaplarından farklıdır. MySQL kullanıcıları, yazılımın veritabanına bağlanması içindir. Sanatçı kullanıcıları ise ileride AspNetUsers tablosunda saklanıp siteden giriş yapacaktır.

   Konteyner içindeki mevcut root parolası kurulumda kullanılır ve konteynerden dışarı çıkarılmaz. Yeni bağlantı bilgileri .local/backend.settings.json içine yazılır. Bu klasör .gitignore kapsamındadır ve site sunucusunun outputs/wwwroot klasörlerinin dışında kalır.

   Yerel geliştirme dosyası şifreli bir kasa değildir. Canlı ortamda bağlantı bilgileri ortam değişkenleri veya Azure Key Vault gibi bir sır yönetimi üzerinden sağlanmalıdır. Ortam değişkenleri, yerel ayar dosyasından daha yüksek önceliğe sahiptir.

   Backend bilgisayarda çalışırken MySQL adresi 127.0.0.1 ve yayımlanmış host portudur. Backend Docker'a alındığında 127.0.0.1 o backend konteynerini gösterir; o aşamada MySQL servis adı ve konteynerin 3306 portu kullanılacak.

9. **HTTP ve Postman örneği**

   GET http://localhost:5000/api/health/live isteği, API işleminin ayakta olduğunu kontrol eder. JSON yanıtında status: "ok" döner. Bu kontrolün başarılı olması MySQL'in de çalıştığını kanıtlamaz.

   GET /api/health/ready, gerçek bağlantıyı ve uygulanmayı bekleyen migration olup olmadığını kontrol eder. İkisi de hazırsa status: "ready", database: "connected", schema: "upToDate" döner. Veritabanı hazır değilse HTTP 503 döner.

   GET /api/health/schema geliştirme ortamında modelin tablo adlarını, uygulanmış migration'ları ve ilişki bilgisini döndürür. Production ortamında bu yol 404 döndürür.

   Controller'daki [Route("api/health")] ile metottaki [HttpGet("live")] birleşerek /api/health/live yolunu oluşturur. Ok(...) HTTP 200 üretir; ASP.NET Core nesneyi JSON'a çevirir. async/await, veritabanından yanıt beklerken sunucu iş parçacığını boş yere bloke etmemeye yardımcı olur.

   Hata yanıtları ProblemDetails biçimindedir: status, title, type ve bu projede eklenen traceId alanı bulunur. traceId, istemcideki hatayı sunucu loglarıyla eşleştirmeye yarar.

   postman/Afterform.Step5.postman_collection.json dosyası Postman'e import edilebilir. Koleksiyondaki baseUrl değişkeni varsayılan olarak http://localhost:5000 değerindedir. Bu koleksiyon yalnızca bu adımın bağlantı kontrollerini içerir; giriş ve demo işlemleri sonraki adımlarda eklenecek.

10. **Çalıştırma sırası**

    Bu bilgisayarda seçilen mevcut konteynerin kısa kimliği `688c9576f123`, MySQL sürümü `8.0.46` ve bilgisayara yayımlanan portu `3306`. İlk hazırlama tamamlandı; root bağlantısı uygulamanın çalışma bağlantısı olarak kullanılmıyor.

    Bağlantı hazırlandıktan sonra normal bir PowerShell penceresinde tek komut:

    ```powershell
    C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Complete-BackendSetup.ps1
    ```

    Bu betik önce Update-Database.ps1 ile migration'ı uygular, ardından Start-Backend.ps1 ile sunucuyu çalıştırır. `Now listening on: http://localhost:5000` görünmesi, sunucunun başladığını gösterir. Pencere açık kalmalıdır; Ctrl+C sunucuyu durdurur. Migration bir kez uygulandığında günlük kullanımda yalnızca Start-Backend.ps1 yeterlidir.

    Komutları ayrı ayrı öğrenmek için proje kökünde:

    ```powershell
    dotnet restore .\backend\Afterform.Api\Afterform.Api.csproj --configfile .\NuGet.config
    dotnet tool restore --configfile .\NuGet.config
    .\scripts\Initialize-Database.ps1 -ContainerName 688c9576f123
    .\scripts\Update-Database.ps1
    .\scripts\Start-Backend.ps1
    ```

    Birden çok MySQL konteyneri varsa Initialize-Database.ps1 komutuna -ContainerName ile doğru konteyner adı verilir. Mevcut afterform veritabanı doluysa -DatabaseName ile bu projeye özel boş bir veritabanı adı seçilir.

    Workbench'te mevcut MySQL bağlantın ile SCHEMAS listesini yenileyebilirsin. database/inspect-schema.sql, tabloları, migration kaydını ve foreign key'i gösteren salt okunur sorguları içerir. Farklı veritabanı adı seçildiğinde dosyadaki USE satırı da o ada göre değiştirilir.

    Bu ajan çalışma ortamında Docker iletişim kanalı erişim reddi verdi. Normal PowerShell'de hazırlama başarılı oldu. Sonraki .NET bağlantısı ise Windows TLS kimlik bilgisi hatası (`0x8009030E`) verdi; ağ izniyle tekrar denemek de sonucu değiştirmedi. Bu yüzden migration ve sunucu normal PowerShell'den başlatılır. Yerel bağlantı ayarındaki `SslMode=Preferred` korunmuştur; TLS kapatılmamıştır. Canlı ortamda sertifika doğrulayan TLS bağlantısı ayrıca yapılandırılmalıdır.

    İlk migration denemesinde `afterform_not_configured` kullanıcısının görünmesi bir ayar yükleme hatasıydı: dosya sağlayıcısının kökü dışındaki yerel JSON dosyası göreli yolla bulunamıyordu. Mutlak yol kullanılarak düzeltildi. EF komutları artık parola yazmadan veritabanı, kullanıcı ve sunucu bilgisini gösterir; beklenen migration kullanıcısı `afterform_migrator` olur.

    Sonradan bir model alanı değiştirildiğinde: yeni migration üret → oluşan değişikliği incele → veritabanına uygula. Mevcut migration dosyalarının sırasını korumak gerekir.

11. **Bu adımdan sonraki öğrenme konusu**

    6. adımda UserManager, SignInManager ve Identity'nin parola hasher'ı devreye girecek. Hazır hesap şemasının üzerine sanatçı hesabı oluşturma, güvenli giriş/çıkış ve oturum yönetimi kurulacak. 7. ve 8. adımlarda gerçek dosya yükleme ve ön yüz–API bağlantısı tamamlanacak.

**İlgili resmi belgeler**

- ASP.NET Core Web API: https://learn.microsoft.com/en-us/aspnet/core/web-api/
- EF Core migration: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- Oracle MySQL EF paketi: https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9
- ASP.NET Core hata yanıtları: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
