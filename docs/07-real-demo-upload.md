# 7. adım — Gerçek demo dosyası yükleme

**Durum: tamamlandı.** 9. adımın normal PowerShell'deki gerçek Newman koşusunda veritabanı/şema readiness kontrolü, üç yapay WAV yüklemesi, sanatçı sahipliği, indirme SHA-256 eşitliği ve byte-range kontrolleri geçti. Güncel büyük dosya testi, açık boyut hatasıyla reddi ve yeni demo kaydı oluşmamasını da doğruladı. Dört test grubu toplam 120 istek / 197 kontrol / 0 başarısızlıkla tamamlandı. Gerçek sanatçı şarkısı veya tarayıcı oynatması ayrıca denenmedi.

Backend ayrı `DemoUploads` derleme yapılandırmasında 0 hata ve 0 uyarıyla derlendi. Yeni form betiği ve ortak giriş betiği `node --check` sözdizimi kontrolünden geçti. EF'nin oluşturduğu ekleme SQL'i `database/DemoFileIntegrity.sql` dosyasında bulunur. 9. adımın gerçek koşusunda API readiness yanıtı veritabanına bağlantıyı ve bekleyen migration olmadığını doğruladı.

Bu adım, mevcut giriş sistemini kullanır. A&R paneli eklemez. Yükleme formu ve gönderim makbuzu gerçek API'yi kullanır. 8. adımın kodunda demo geçmişi de gerçek kayıtlara bağlandı ve ayrı ayrıntı sayfası eklendi; etkinleştirme ve kullanım için `docs/08-demo-history.md` rehberine bak.

## 1. Etkinleştir

Docker'daki mevcut `688c9576f123` MySQL konteyneri açık olmalı. Yeni konteyner veya sanatçı hesabı oluşturman gerekmiyor.

1. Backend'in çalıştığı normal PowerShell penceresinde `Ctrl+C` ile sunucuyu durdur.
2. Şu komutu çalıştır:

```powershell
& "C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Complete-BackendSetup.ps1"
```

Bu komut güncel backend'i derler, bekleyen migration'ı uygular ve localhost:5000 üzerinde sunucuyu açar. Migration yalnızca `Demos` tablosuna `FileSha256` ve `RightsConfirmedAtUtc` alanlarını ekler. Mevcut sanatçı hesapları korunur. Sonunda `Now listening on` görünmeli; bu pencere açık kalmalı.

Bu çalışma ortamında Docker iletişim kanalı ve .NET–MySQL Windows TLS erişimi kısıtlandığından, migration ve çalışan sunucu normal kullanıcı terminalinde yönetilir. 9. adımın normal terminaldeki gerçek koşusu veritabanı bağlantısını ve güncel şemayı doğruladı.

Sonraki günlük kullanımda yalnızca `scripts/Start-Backend.ps1` yeterlidir. Parolanı veya bağlantı dosyasını paylaşmana gerek yok.

## 2. Siteden gerçek dosya gönder

1. [Giriş sayfasını](http://localhost:5000/artist/login/) aç; mevcut sanatçı hesabınla giriş yap.
2. [Send a Demo](http://localhost:5000/artist/submit/) sayfasına git. Eski sayfa görünüyorsa `Ctrl+F5` ile yenile.
3. Şarkı adını ve türünü seç. İsteğe bağlı en fazla 500 karakter not ekle.
4. Kendi bilgisayarındaki **tek bir MP3, WAV veya FLAC** dosyasını seç veya sürükle.
5. Kaydı paylaşma hakkın olduğunu onayla ve **Send demo** düğmesine bas.

Dosya sınırı arayüzde 100 MB olarak gösterilir; uygulamadaki kesin sınır **104.857.600 bayt (100 MiB)**. WAV için PCM ve IEEE float, bunların WAVEFORMATEXTENSIBLE biçimleri desteklenir. Sıkıştırılmış WAV, RF64, Ogg içine alınmış FLAC ve free-format MP3 bu sürümde desteklenmez. Dosyanın uzantısını değiştirerek uygun hale getiremezsin; gerekirse ses uygulamandan desteklenen biçimde yeniden dışa aktar.

Aktarım yüzdesi yalnızca tarayıcıdan giden veriyi gösterir. %100'den sonra sunucu doğrulama ve kayıt işlemlerini bitirir. Başarı mesajı yalnızca API'nin **201 Created** yanıtından sonra gösterilir.

Başarılı gönderimde:

- Gönderim numarası ve başlangıç durumu **Submitted** görünür.
- Kaydını dinleme ve orijinal dosyayı indirme bağlantısı açılır.
- Adres `/artist/submit/?demo=123` benzeri bir makbuz bağlantısına dönüşür. Kaydedip daha sonra kendi hesabınla açabilirsin.
- Form temizlenir; başka bir demo gönderebilirsin.

Tarayıcı bazı ses biçimlerini oynatamayabilir. Oynatıcıdaki hata, dosyanın mutlaka kaydedilmediği anlamına gelmez; orijinal dosyayı indirme seçeneği ayrıca vardır. Oturum biterse dosyaya yeniden erişmek için giriş yapmalısın. Güncel 8. adım sürümünde **View all my demos** ile gerçek geçmişi, **View demo details** ile ayrı ayrıntıyı açabilirsin.

## 3. Dosya ve MySQL kaydı nerede?

| Veri | Konum / görev |
|---|---|
| Sesin orijinal baytları | `C:\Users\Windows11\Documents\Codex\2026-09-27\sen\.local\uploads\demos` |
| Yükleme sırasında hazırlanan dosya | Aynı klasörün `.pending` alt klasörü; doğrulama sonrası taşınır |
| Sanatçı, başlık, tür, not, durum, tarih | MySQL `afterform.Demos` tablosu |
| Özel dosya anahtarı, orijinal ad, boyut, tür, SHA-256 | Aynı demo kaydı |
| Paylaşım hakkı onayının zamanı | `RightsConfirmedAtUtc`; beyanı kaydeder, hak sahipliğini kanıtlamaz |

Dosyalar `.local` altında olduğu için Git'e eklenmez; `outputs` veya `wwwroot` içinde bulunmaz. Dosyaya rastgele bir sunucu adı verilir. Kullanıcının verdiği ad yalnızca gösterim ve indirme metaverisi olarak tutulur.

MySQL konteyneri dosyanın tamamını saklamaz. Bu aşamada dosya klasörü, backend'in çalıştığı bilgisayardadır. Sonraki Docker aşamasında dosyalar için ayrı kalıcı volume; Azure aşamasında Blob Storage değerlendirilecek. Veritabanı ile ses klasörü birlikte yedeklenmelidir.

Üretimde `DemoStorage:RootPath` açıkça yapılandırılmalıdır. Ortam değişkeni karşılığı `DemoStorage__RootPath` olur. Uygulama, kendi uygulama ve açık ön yüz klasörleriyle çakışan depolama yollarını reddeder.

## 4. Sunucu hangi kontrolleri yapıyor?

- **Kimlik:** Tüm demo API uç noktaları giriş ister. Sahiplik, oturumdaki kullanıcı kimliğinden alınır; formdan `ArtistId` kabul edilmez.
- **CSRF:** Yükleme isteği `X-AF-CSRF` başlığındaki geçerli token'ı gerektirir.
- **Dosya sayısı ve boyutu:** Tek dosya, sıfırdan büyük boyut, en fazla 100 MiB. İstek gövdesi ve multipart alanları da sınırlıdır.
- **Tür:** Uzantı izin listesi ve dosya yapısı kontrol edilir. Tarayıcının `Content-Type` beyanı güvenilir kabul edilmez. MP3'te ID3 bilgisi sonrası art arda üç MPEG Layer III frame; WAV'da RIFF/WAVE, biçim ve veri parçaları; FLAC'ta STREAMINFO, metadata sınırları ve ilk frame başlığı incelenir.
- **Alanlar:** Başlık 100, tür 64, not 500 karakter sınırı; izin verilen tür ve paylaşım onayı kontrol edilir.
- **İstek sınırı:** Sanatçı hesabı başına 10 dakikada 6 yükleme isteği. Hatalı yükleme denemeleri de bu sayıya dahildir. Sayaç bu sunucu sürecinin belleğindedir.
- **Dosya erişimi:** Bilgi, dinleme ve indirme isteklerinde kayıt kimliği ile oturumdaki sanatçı birlikte sorgulanır. Başka sanatçının kaydı ve bulunamayan kayıt için aynı `404` yanıtı verilir.
- **Sunum:** Dosya sunucunun belirlediği ses türüyle, `nosniff` ve `private, no-store` başlıklarıyla gönderilir. Oynatıcı için byte-range istekleri desteklenir. Kullanıcı metinleri ön yüzde `textContent` ile gösterilir.

Kontroller, [Microsoft dosya yükleme rehberi](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0) ve [OWASP yükleme rehberindeki](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) ilkelerden yararlanır. Biçim ayrıntıları için [FLAC RFC 9639](https://www.rfc-editor.org/rfc/rfc9639.html), [WAVEFORMATEXTENSIBLE](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible) ve [FFmpeg MPEG ses başlığı uygulaması](https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/mpegaudiodecheader.c) incelendi.

### Bu kontrollerin sınırı

Yapısal inceleme tam ses çözümlemesi veya antivirüs taraması değildir. SHA-256 da dosyanın zararsız olduğunu kanıtlamaz. Bu adımda antivirüs, dosya sistemi execute ACL yapılandırması, toplam depolama kotası veya birden fazla sunucu için ortak rate limiter eklenmedi. Yayınlama aşamasında kaynak sınırları ve dosya tarama politikası ayrıca ele alınmalıdır.

Dosya sistemi ile MySQL arasında ortak bir ACID transaction yoktur. Normal hata durumlarında geçici dosya temizlenir. Veritabanı yazma sonucu belirsizse, dosyayı silmeden önce kaydın bulunmadığı doğrulanmaya çalışılır; veritabanına ulaşılamıyorsa olası başarılı kaydın dosyasını silmemek için dosya tutulur ve log yazılır. İşlem/sunucu aniden kapanırsa kayıtsız dosya kalabilir; üretimde uzlaştırma/temizlik işi gerekir. Yükleme yanıtı ağda kaybolursa başarı belirsiz olabilir; aynı kaydı hemen yeniden gönderme.

ASP.NET Core, `IFormFile` model binding sırasında büyük dosyaları geçici diskte buffer edebilir. Özel depolamaya yazarken 64 KiB bloklarla okunur ve SHA-256 aynı geçişte hesaplanır; tüm ses belleğe alınmaz. İleride yoğun trafik için doğrudan streaming veya Blob yüklemesi değerlendirilebilir.

## 5. Workbench'te gönderimi incele

Workbench'te mevcut MySQL bağlantını aç ve `afterform` şemasını yenile. [inspect-demos.sql](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/database/inspect-demos.sql) dosyasını SQL sekmesinde açıp içindeki okuma sorgularını kullanabilirsin.

Gönderdiğin kayıtta `Title`, `OriginalFileName`, `FileSizeBytes`, `SubmittedAtUtc` ve sanatçı kullanıcı adını göreceksin. İlk durum `Submitted` olur. `FileSha256`, tüm orijinal dosyanın SHA-256 özetidir. `RightsConfirmedAtUtc`, onayın UTC zamanıdır.

`DemoFileIntegrity` migration'ı öncesinden kayıt varsa yeni alanlar boş hash / null onay zamanı olabilir. Yeni gerçek yüklemelerde bu alanlar doldurulur.

### SHA-256'yı kendin nasıl kullanabilirsin?

Kendi PowerShell'inde dosyanın yolunu ver:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath "C:\kendi\dosyan\demo.wav"
```

Çıktıdaki hash ile MySQL'deki `FileSha256` değeri, büyük/küçük harf farkı dışında aynı olmalı. İndirilen orijinal dosyada da aynı komutu kullanabilirsin. Kod hash'i yüklemede hesaplayıp saklar; her indirmede tekrar bütünlük taraması yapmaz. Dosya özeti ile Identity'nin tuzlanmış parola hash'i farklı amaçlara hizmet eder.

## 6. Postman'de gerçek dosya gönder

[Afterform.Step7.postman_collection.json](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/postman/Afterform.Step7.postman_collection.json) dosyasını Import ile içeri al. `baseUrl` localhost:5000; `userName` kendi sanatçı adın; `password` yalnızca kendi Postman kullanımında dolduracağın boş değişkendir. Authorization **No Auth**, cookie jar açık olmalı.

İstekleri sırayla kullan:

1. **Giriş öncesi CSRF:** Token ve ilişkili çerez alınır.
2. **Giriş:** Cookie oturumu açılır.
3. **Yükleme seçenekleri:** Kabul edilen uzantı ve boyut sınırı görülür.
4. **Giriş sonrası CSRF:** Artık sanatçı oturumuna ait token alınır.
5. **Demo yükle:** Body → form-data içinde `audioFile` satırının türü **File**. Bu satırda **Select Files** ile kendi bilgisayarındaki kaydı seç. Diğer satırlar Text: `trackTitle`, `genre`, `artistNote`, `rightsConfirmation=true`.
6. **Gönderim bilgileri:** Yükleme yanıtından alınan `demoId` ile kendi kaydını aç.
7. **Ses:** Kendi dosyanın ses yanıtını al.
8. **Orijinali indir:** Postman sürümündeki **Send and Download / Save Response** seçeneğini kullanabilirsin.

Yükleme isteğine `Content-Type` başlığını elle ekleme. Postman, form-data için multipart boundary üretir. Dosyayı raw JSON veya Base64 olarak göndermiyoruz. `demoId` ve token'ı alan kısa post-response betikleri yalnızca değişken aktarır; test assertion içermez. Koleksiyon ajan tarafından çalıştırılmadı.

Parola doldurulmuş koleksiyonu dışa aktarma veya paylaşma. Tarayıcıdaki giriş ile Postman cookie oturumu ayrıdır. Koleksiyon, varsayılan örnek parolayla giriş yapmaz; kendi parolan gereklidir.

## 7. İstek ve yanıt özeti

| Adres | Görev |
|---|---|
| `GET /api/demos/options` | Oturumla yükleme sınırları |
| `POST /api/demos` | Multipart dosya + alanlar; cookie + CSRF |
| `GET /api/demos/{id}` | Yalnızca kendi kaydının metaverisi |
| `GET /api/demos/{id}/audio` | Yalnızca kendi sesin; byte-range destekli |
| `GET /api/demos/{id}/audio?download=true` | Orijinal dosyayı indirme |

| Yanıt | Anlam |
|---|---|
| `201` | Dosya ve demo kaydı kaydedildi |
| `400` | Alan, dosya yapısı veya CSRF doğrulaması başarısız; multipart okuma/boyut hatası MVC doğrulamasına aktarılırsa da bu yanıt gelebilir |
| `401` | Oturum yok / bitmiş |
| `404` | Kayıt yok, dosya bulunamadı veya bu hesaba ait değil |
| `413` | İstek veya dosya sınırı aşılmış |
| `429` | Yükleme istek sınırı aşılmış |
| `503` | MySQL veya özel depolama geçici olarak kullanılamıyor |

IIS/reverse proxy/hosting ayrıca daha düşük istek sınırı uygulayabilir; yayınlama aşamasında bu sınırlar da ayarlanmalıdır. Şu an Kestrel ve uygulama sınırları bu yerel akış için yapılandırıldı.
