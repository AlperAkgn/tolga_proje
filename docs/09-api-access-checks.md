# 9. adım — Postman ile sanatçı erişimi ve hata senaryoları

**Durum: tamamlandı.** Kullanıcı 2 Ekim 2026'da normal PowerShell'de Postman koleksiyonunu Newman ile yeniden çalıştırdı: **120 istek / 197 kontrol / 0 başarısızlık**. Dört grubun tamamı gerçek ASP.NET Core API ve mevcut Docker MySQL üzerinde geçti. Kontrollerin 195'i Postman assertion'ı, ikisi gerçek dosya baytları üzerinde SHA-256 ve Range karşılaştırmasıdır. Ajan, terminal çıktısını ve üretilen JSON/Markdown raporlarını okuyarak sonucu doğruladı; Postman masaüstü arayüzünden ayrı bir koşu yapılmadı. Güncel gerçek sonuç [09-last-run.md](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/docs/09-last-run.md) dosyasında.

| Grup | HTTP isteği | Kontrol | Başarısızlık |
|---|---:|---:|---:|
| Oturum ve sahiplik | 63 | 110 | 0 |
| Yükleme doğrulaması | 30 | 46 | 0 |
| Hesap kilitlenmesi | 15 | 27 | 0 |
| Giriş istek sınırı | 12 | 14 | 0 |
| **Toplam** | **120** | **197** | **0** |

Backend ayrı `SecurityChecks` yapılandırmasında **0 hata / 0 uyarı** ile derlenmişti. Değişen Node betikleri ve koleksiyondaki 146 script'in sözdizimi kontrolü başarılı. Bu düzeltmede backend kodu ve veritabanı şeması değiştirilmedi.

### İlk koşudaki 400/413 farkı

ASP.NET Core'un form okuyucusu, multipart gövdeyi okurken oluşan `InvalidDataException` veya `IOException` hatasını MVC model doğrulamasına aktarabilir. `[ApiController]` bu durumda 400 yanıtı üretir; büyük gövde her katmanda doğrudan 413 olarak dönmeyebilir. Kaynak kodundaki [FormValueProviderFactory](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Core/src/ModelBinding/FormValueProviderFactory.cs) ve [CompositeValueProvider](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Core/src/ModelBinding/CompositeValueProvider.cs) bu aktarımı gösterir. İlk koşuda 400 gözlendi; yanıt gövdesi raporda saklanmadığı için boyut hatasının ayrıntısı o koşuda ayrıca kaydedilmedi.

Güncel boyut testi:

- Yalnızca **400 veya 413** yanıtlarını kabul eder.
- 400 gelirse doğrulama hatasında **boyut sınırına ait açık bir mesaj** bulunmasını da ister. CSRF, tür veya başka bir alan hatasıyla gelen rastgele 400 yeterli değildir.
- Başarısız yüklemelerden sonra veritabanında yeni demo oluşmadığını kontrol etmeye devam eder.
- Büyük girdiyi geçerli PCM WAV başlığı ve sessiz veriyle üretir. Böylece bozuk dosya biçimi, boyut kontrolü çalışmasa bile testi başarılı gösteremez.

İlk koşuda SHA-256 indirme eşitliği, byte-range eşitliği, karşılıklı sanatçı sahipliği, CSRF ve kayıt sayısı kontrolleri başarılıydı. Güncel koşuda düzeltilmiş boyut testi ve kalan kilitlenme/istek sınırı grupları da geçti. Node `DeprecationWarning` satırları test başarısızlığı değildir.

## 1. Testleri tek komutla çalıştır

Docker ve mevcut `688c9576f123` MySQL konteyneri açık olmalı. 7. adımın migration'ı uygulanmış olmalı. **Kendi normal PowerShell pencerende** çalıştır:

```powershell
& "C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Test-ArtistSecurity.ps1"
```

Bu betik:

1. Backend'i `SecurityChecks` isimli ayrı build klasöründe derler. Günlük sunucunun Debug dosyasını değiştirmez.
2. `http://127.0.0.1:5019` üzerinde ayrı bir test sunucusu açar. 5000 portuna dokunmaz; 5019 doluysa başka işlemi kapatmadan hata verir. Gerekirse komutun sonuna `-Port 5020` ekleyebilirsin.
3. Aynı proje veritabanında benzersiz isimli iki **QA hesabı** oluşturur. Mevcut sanatçı hesabını veya parolasını kullanmaz. Rastgele test parolası yalnızca işlem belleğinde ve CLI stdin aktarımında kullanılır; dosyaya, argümanlara veya rapora yazılmaz.
4. Bir saniyelik düşük sesli PCM WAV üretir. Başarılı yüklemeler yalnızca bu yapay kaydı kullanır; kendi şarkını seçmen gerekmiyor.
5. Geçersiz dosya ve alan örneklerini, sahiplik, CSRF, kilitlenme ve rate limit senaryolarını Newman ile yürütür.
6. Her grup için kendi test sunucusunu yeniden başlatır. Böylece sürece ait istek sayaçları önceki gruptan taşınmaz. Hesap kilidi veritabanında kaldığından kilitlenme grubu sona yakındır.
7. Sonunda yalnızca kendi açtığı test sunucusunu durdurur; parola içermeyen sonuç raporunu yazar.

Çıktıda `[1/5]`–`[5/5]`, HTTP kodları ve `RESULT` göreceksin. Bu komut bittikten sonra terminalde sürekli `Now listening on` bekleme; test sunucusu geçicidir. 10. adımdan sonra günlük site Docker'da 8080 portunda `Start-BackendDocker.ps1` ile açılır. Bu test betiği ise Windows'ta 5019 portunda ayrı bir .NET sunucusunu çalıştırmayı sürdürür; Docker backend'ini doğrudan test etmez.

### Neden normal PowerShell gerekiyor?

Önceki kurulumda bu çalışma ortamının .NET–MySQL bağlantısı Windows TLS/SSPI kısıtına takıldı. Bu adımda burada başlatılan test sunucusunun readiness yanıtı da 503 oldu. 503 tek başına TLS hatasını kanıtlamaz; Docker, bağlantı veya bekleyen migration da sebep olabilir. Normal terminalde çalıştırma aynı kısıtı aşmak için mevcut proje yaklaşımıdır. Hata devam ederse rapordaki hazırlık aşamasını birlikte inceleyebiliriz.

## 2. Sonucu nasıl okuyacaksın?

[09-last-run.md](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/docs/09-last-run.md) her koşuda yenilenir. Ayrıntılı, yine parola içermeyen JSON `.local/security-checks/latest-result.json` altında tutulur.

| Durum | Anlam |
|---|---|
| `passed` | Dört grubun tamamı çalıştı; kaydedilen assertion ve ikili dosya kontrolleri geçti |
| `failed` | Senaryolar başladı; en az bir kontrol veya çalışma hatası var. Başarısız grup/istek adına bak |
| `blocked` | Hazırlık tamamlanamadı; güvenlik kontrolleri başlamadı. Sıfır başarısızlık, başarı anlamına gelmez |

Başarısızlıkta çalıştırıcı sonraki gruplara geçmez. Örneğin gerçek MySQL liste sorgusu hata verirse diğer kontrolleri geçti diye göstermez. Test raporu standart Newman JSON raporunun tamamını dışa aktarmaz: standart çıktı, istek gövdeleri, parolalar ve çerezler içerebilir. Burada yalnızca istek/test adı, HTTP kodu, sonuç sayıları ve QA kullanıcı/kayıt kimlikleri saklanır.

## 3. Kontrol ettiğimiz senaryolar

| Grup | Deneme | Beklenti |
|---|---|---|
| Oturum yok | Liste, ayrıntı, ses, indirme, yükleme | `401` |
| Özel HTML | Girişsiz ayrıntı sayfası | Giriş sayfasına `302` |
| CSRF | Eksik veya geçersiz login token | `400` |
| Giriş | Olmayan kullanıcı | Genel `401`; hesap var/yok ayrıntısı yok |
| Oturum | Doğru giriş, profil, çıkış | `200`, sonra `204`, sonra `401` |
| Cookie | Başarılı giriş | HttpOnly, SameSite=Lax, Path=/; yanıt parola/hash döndürmez |
| Gerçek yükleme | Yapay WAV | `201`, doğru sahip, `Submitted`, doğru boyut ve SHA-256 |
| İndirme | Orijinal WAV | `200`; indirilen baytların SHA-256 özeti kaynakla aynı |
| Dinleme | İlk 16 bayt için Range | `206`; yanıt tam kaynak dilimine eşit |
| Sahiplik | B, A'nın ayrıntı/ses/indirme kimliğini ister | Her biri `404` |
| Sahiplik | A, B'nin kimliğini ister | Her biri `404` |
| Sahiplik | URL'ye başka `artistId` eklemek | Oturum hesabının listesi değişmez |
| Alan taklidi | Yüklemede `ArtistId=A`, `Status=Accepted` gönderen B | Sahip B, durum `Submitted` kalır |
| Liste | Arama, durum, büyük geçerli sayfa | Gerçek kayıtlar, doğru sayaçlar, son sayfaya sınırlama |
| Sorgu hatası | Sayfa 0, boyut 51, bilinmeyen/nümerik durum, uzun arama | `400` |
| SQL benzeri metin | Aramaya `' OR 1=1 --` yazmak | Normal arama; eşleşme yok, sahiplik aşılmaz |
| Dosya hatası | Dosya yok, boş WAV, .txt, WAV diye adlandırılmış metin | `400` |
| Alan hatası | Onay false, boş/uzun başlık, uygunsuz tür, uzun not | `400` |
| Dosya sayısı/boyutu | İki dosya / uygulama istek sınırından büyük geçerli WAV | `400` / `400` veya `413`; 400 için açık boyut hatası gerekir |
| Yükleme sayacı | Altı başarısız denemeden sonra yedinci | `429`; geçersiz denemeler de sayılır |
| Kayıt bütünlüğü | Başarısız yüklemeler sonrası liste | Kayıt sayısı artmaz |
| Hesap kilidi | B'ye beş yanlış parola, sonra doğru parola | Giriş hâlâ `401`; A kendi parolasıyla girebilir |
| Login IP sayacı | Olmayan kullanıcıya 10 deneme, 11. deneme | İlk 10 `401`, 11. `429` + Retry-After |

İndirme özeti ve Range bayt eşitliği Node çalıştırıcısında gerçek yanıt Buffer'ı üzerinden ayrıca kontrol edilir. Koleksiyondaki testler de HTTP, metaveri, başlık ve sahiplik kontrollerini içerir.

Bu sonuç, yukarıdaki API senaryolarının geçtiğini gösterir; kapsamlı sızma testi değildir. Gerçek sanatçı şarkısı, tarayıcıdaki görsel akış ve ses oynatması, üretim HTTPS/Secure-cookie, dağıtık rate limiter, oturum çalınması, bütün XSS yolları, antivirüs ve tüm MP3/FLAC çeşitleri burada doğrulanmadı. Text olarak tutulan örnek notun API'den değişmeden gelmesi, tek başına tarayıcıda XSS testi değildir. Hesap kilidinin doğru parolayı da engellediği gözlendi; 15 dakikalık sürenin dolması beklenip yeniden giriş ayrıca denenmedi.

## 4. Workbench'te ne görebilirsin?

Başarılı hazırlık sonrası raporda `qa9.a...` ve `qa9.b...` kullanıcı adları bulunur. [inspect-security-checks.sql](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/database/inspect-security-checks.sql) içindeki iki yer tutucuyu bu adlarla değiştir.

- QA A için bir demo, QA B için iki demo beklenir; hepsi `Submitted` olmalıdır.
- Reddedilen yüklemeler yeni kayıt oluşturmaz.
- Kilitlenme kontrolü başarılıysa B'nin `LockoutEnd` değeri kilitleme anından 15 dakika sonrasını gösterir. Sonradan baktığında bu süre dolmuş olabilir. Identity kilitlenme eşiğinde `AccessFailedCount` değerini sıfırlayabilir; bu alanın 0 olması hesabın açık olduğunu göstermez. `LockoutEnd` ile değerlendir.
- İki QA hesabına bilerek **aynı rastgele parola** verilmiştir. `HashesDiffer=1` sonucu, kayıtların farklı hash kodlamaları taşıdığını gösterir; bu davranış Identity'nin kullanıcı başına rastgele salt kullanmasıyla uyumludur. Tam hash veya salt ekrana alınmaz.

QA hesapları ve başarılı yapay demo kayıtları incelemek için tutulur. Otomatik parolalar raporda bulunmadığından bu hesaplara elle giriş yapmak için kullanılmazlar. Her koşu yeni benzersiz hesaplar üretir; mevcut kullanıcı adı veya parola değiştirilmez. Dosyalar API'nin mevcut özel depolamasına kaydedilir. Büyük geçersiz boyut girdisi koşu sonunda silinir; diğer küçük kaynak dosyalar `.local/security-checks/<run>/` altında kalır.

## 5. Postman arayüzünde öğrenerek kullanma

Otomatik çalıştırıcı, Postman koleksiyonunu terminalden yürüten **Newman** kullanır. Newman yalnızca `tests/postman` altında kuruludur; backend'in çalışma bağımlılığı değildir. Resmî [Newman kullanım rehberi](https://learning.postman.com/docs/reference/newman-cli/installing-running-newman) ve [Node API açıklaması](https://github.com/postmanlabs/newman#api-reference) bu yaklaşımı destekler.

Postman'de kendin çalıştırmak istersen:

1. [Step9 koleksiyonunu](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/postman/Afterform.Step9.postman_collection.json) ve [boş ortam şablonunu](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/postman/Afterform.Step9.postman_environment.json) Import ile al. Sağ üstten bu **environment**'ı seç.
2. Önce öğrenme amaçlı ayrı iki QA hesabı oluştur; yalnızca bu hesapların parolalarını `passwordA/passwordB` ortam değişkenlerine yerel olarak yaz. Günlük sanatçı hesabını kilitlenme grubunda kullanma. Hesap oluşturmak için mevcut `Create-Artist.ps1 -UserName qa9.manual.a -DisplayName "QA Artist A"` aracını kullanabilirsin; mevcut kullanıcıyı değiştirmez.
3. Bu hesabın adını `userNameA/B` olarak gir. Başlangıç koleksiyonu taze, henüz demo göndermemiş hesaplar bekler.
4. Ayrı test backend'i 5019 portunda aç: normal terminalde `Start-Backend.ps1 -Port 5019`. Docker MySQL açık olsun.
5. Ortamdaki `validFile` yoluna küçük WAV, `fileSizeBytes/fileSha256` alanlarına dosyanın gerçek boyutu ve SHA-256 değerini yaz. Diğer dosya yollarını hatalı küçük dosyalara ayarla. Oversized dosyayı otomatik araç hazırladığı için o senaryoyu elle yapacaksan kontrollü boyut girdisi gerekir. Body → form-data satırlarının File olduğuna bak.
6. Cookie jar açık, Authorization **No Auth** olmalı. **01 Sessions and ownership** klasörünü seçip **Run folder** ile bir kez çalıştır. Scripts → Post-response altında `pm.test(...)` assertion'larını, yanıtın **Test Results** bölümünde geçip geçmediğini görebilirsin.
7. **Her sonraki klasörden önce** yalnızca test backend'ini Ctrl+C ile durdurup tekrar başlat. Klasörleri topluca art arda günlük sunucuda çalıştırma: önceki denemeler rate limit'e sayılır ve yanlış senaryoda 429 üretir. Sıra 01 → 02 → 03 → 04 olmalı; 03'ten sonra B 15 dakika kilitlidir.
8. Ortam parolalarını kullandıktan sonra temizle. Parola doldurulmuş environment, cookie jar veya Runner geçmişini paylaşma.

Yeni bir manuel çalıştırma için yeniden iki taze QA hesabı gerekir: ana grup boş hesapla başlar ve gerçek dosya yükler. Otomatik araç bu hazırlığı her koşuda kendi yapar.

## 6. Hata gelirse

- **Hazırlık 503:** Önce Docker/MySQL ve migration durumunu kontrol et; betiği normal PowerShell'de çalıştırdığından emin ol.
- **5019 dolu:** `-Port 5020` seç. Araç mevcut işlemi kapatmaz.
- **401 beklerken 400:** Login CSRF token'ı ve cookie eşleşmesini kontrol et.
- **Boyut testinde 400:** Doğrulama hatası boyut sınırını belirtmelidir. Güncel test bu koşulu ayrıca kontrol eder; bir başka hata sebebiyle gelen 400 geçmez.
- **400/413 beklerken 429:** İstek sayacı daha önce dolmuş olabilir; ayrı test sunucusunu yeniden başlatıp taze gruptan başla.
- **Yükleme 201 yerine 503:** Dosya özel depolaması ve gerçek MySQL kaydı için API tarafını incelememiz gerekir.
- **Sahiplik kontrolünde 200:** Ciddi test başarısızlığıdır; rapordaki isteği ve sanatçı bağlamını inceleyip sunucu sorgusunu düzeltmeden ilerlemeyiz.

Bu adım başarılı gerçek koşu sonucuyla kapatıldı. 10. adım, backend'i Docker'a alma ve veritabanı/dosya kalıcılığıdır; henüz başlanmadı.
