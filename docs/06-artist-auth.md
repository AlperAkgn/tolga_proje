# 6. adım — Sanatçı hesabı ve gerçek giriş

Bu adımda sanatçı hesabı oluşturma, parola hash'i ve salt, giriş, oturum ve çıkış altyapısı eklendi. Hesaplar label tarafından verilir; herkese açık kayıt ekranı yoktur. Demo listesi ve demo gönderim ekranı hâlâ örnek/önizleme verilerini kullanır. Gerçek dosya yükleme 7. adımda, demo verilerinin API'ye bağlanması 8. adımda yapılacak.

**Çalıştırma durumu:** 6. adım etkinleştirildi. Kullanıcı hesap oluşturma ve sunucu başlatma betiğini normal PowerShell'den çalıştırdı ve `Now listening on` çıktısını bildirdi. Güncel API'nin `/api/health/ready` yanıtı `ready / connected / upToDate`; `/api/health/schema` Identity cookie oturumunun etkin olduğunu bildiriyor. Aşağıdaki komutlar normal PowerShell'de kullanılır; bu çalışma ortamının Windows TLS kısıtı nedeniyle MySQL hesabı oluşturma işlemi burada çalıştırılamıyor.

Backend ayrı bir yerel derleme klasörüne 0 hata ve 0 uyarı ile derlendi. Giriş/çıkış için otomatik test çalıştırılmadı. Sanatçı parolasını kullanıcı belirledi; ajan parolayı almadı veya hesapla giriş yapmadı. İlk hesabı tekrar oluşturmak yerine aşağıdaki günlük başlatma komutunu kullan.

**Giriş hatası düzeltmesi:** İlk canlı giriş isteğinde HTTP 500 görüldü. `[ValidateAntiForgeryToken]` filtresi için gereken MVC ViewFeatures servisleri `AddControllers()` ile kaydedilmiyordu. Başlangıç kaydı `AddControllersWithViews()` olarak düzeltildi; API uç noktaları JSON döndürmeye devam eder. Bu düzeltmenin etkinleşmesi için çalışan sunucu durdurulup Start-Backend.ps1 ile yeniden başlatılmalıdır. Ön yüzde HTTP 500 artık bağlantı kopması yerine sunucu hatası olarak gösterilir.

Kullanıcı sunucuyu güncel kodla yeniden başlattı. Var olmayan rastgele kullanıcı adıyla yapılan aynı teşhis isteği, CSRF cookie ve header ile artık HTTP 500 yerine beklenen HTTP 401 yanıtını döndürüyor. Bu işlem gerçek sanatçı hesabına giriş denemedi ve sanatçının parolasını kullanmadı. Ardından kullanıcı, kendi tarayıcısında kendi parolasıyla giriş yaptı ve portalın açıldığını doğruladı. Giriş hatası giderildi ve 6. adım tamamlandı.

**Günlük kullanım:**

```powershell
C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Start-Backend.ps1
```

Sunucu açıkken http://localhost:5000/artist/login/ adresinde varsayılan `mira.sol` kullanıcı adı ve belirlediğin parola ile giriş yapabilirsin. Farklı hesap adı seçtiysen onu kullan. Aşağıdaki hesap oluşturma bölümü yeni sanatçılar eklemek için de geçerlidir.

**Tek komutla etkinleştirme:** Eski sunucunun PowerShell penceresinde Ctrl+C yaptıktan sonra aşağıdaki komutu çalıştır:

```powershell
C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Enable-ArtistAccess.ps1
```

Bu betik `mira.sol` / `Mira Sol` hesabı için parolayı gizli olarak iki kez sorar, hesabı oluşturur ve güncel sunucuyu başlatır. Farklı bir hesap adı için `-UserName`, görünen ad için `-DisplayName` parametrelerini kullanabilirsin. Hesap zaten varsa parolası değiştirilmez; günlük kullanım için yalnızca Start-Backend.ps1 çalıştırılır. Aşağıdaki bölümler aynı işlemleri ayrı ayrı açıklar.

## 1. Hazırlık: Docker ve backend

Docker Desktop açık, mevcut MySQL konteynerin çalışır durumda olmalı. PowerShell'de kontrol et:

```powershell
docker ps --filter id=688c9576f123
```

`STATUS` alanında `Up` görünmeli. Bu adım 5. adımda hazırladığımız `afterform` veritabanını kullanır.

Önceki backend'in çalıştığı PowerShell penceresinde `Ctrl+C` ile sunucuyu durdur. Ardından aşağıdaki hesap oluşturma işlemini yap. İşlem bittikten sonra backend'i yeniden açacağız.

## 2. İlk sanatçı hesabını oluştur

PowerShell'e şu satırı aynen yapıştır:

```powershell
& "C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Create-Artist.ps1" -UserName mira.sol -DisplayName "Mira Sol"
```

Betik parolayı terminalde gizli olarak ister. Parolayı komutun sonuna yazma; sorulduğunda gir. Bu sayede parola komut geçmişine eklenmez. Kullanacağın parolayı kendin seç; örnek bir parolayı bütün hesaplarda tekrar kullanma.

Hesap bilgileri için kurallar:

| Bilgi | Kural |
|---|---|
| Kullanıcı adı | 1–64 karakter; `a-z`, `A-Z`, `0-9` ve `-._@+` |
| Görünen sanatçı adı | En fazla 120 karakter; örneğin `Mira Sol` |
| Parola | 15–128 karakter; yalnızca boşluk olamaz |

Parolada büyük harf, rakam veya özel karakter zorunlu değildir. Uzun, sana özel bir parola cümlesi kullanabilirsin. Kullanıcı adı ile görünen ad farklı görevler görür: girişte `mira.sol`, portalda görünen sanatçı adı için `Mira Sol` kullanılır.

Bu işlem `AspNetUsers` tablosuna bir sanatçı hesabı ekler. `afterform_app` ve `afterform_migrator` ise MySQL bağlantı kullanıcılarıdır; sanatçı giriş ekranında kullanılmazlar.

## 3. Backend'i yeniden başlat ve siteden giriş yap

```powershell
& "C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Start-Backend.ps1"
```

`Now listening on: http://localhost:5000` görünürse bu pencereyi açık bırak.

Tarayıcıda [sanatçı giriş ekranını](http://localhost:5000/artist/login/) aç. Oluşturduğun kullanıcı adını ve parolanı gir. Başarılı girişten sonra sanatçı portalı açılır ve kendi hesap adın gösterilir. Çıkış bağlantısı oturumu sonlandırır.

Bu aşamada gördüğün örnek demo kayıtları MySQL'den gelen gerçek gönderimler değildir. Demo formunda dosya seçmek de henüz kalıcı dosya yüklemez.

## 4. Workbench'te hesabı incele

Workbench'te mevcut `127.0.0.1:3306` bağlantını aç. `SCHEMAS` listesini yenile; `afterform` veritabanını bul. Yeni SQL sekmesine şu sorguyu yazıp çalıştır:

```sql
SELECT
    UserName,
    DisplayName,
    CASE WHEN PasswordHash IS NOT NULL AND LENGTH(PasswordHash) > 0
         THEN 1 ELSE 0 END AS HasPasswordHash,
    LENGTH(PasswordHash) AS PasswordHashLength,
    AccessFailedCount,
    LockoutEnd
FROM afterform.AspNetUsers
WHERE UserName = 'mira.sol';
```

Sonuçtaki alanları şöyle oku:

| Alan | Ne öğrenirsin? |
|---|---|
| `UserName` | Girişte kullanılan hesap adı |
| `DisplayName` | Sanatçının görünen adı |
| `HasPasswordHash` | `1` ise kayıtlı parola hash'i var |
| `PasswordHashLength` | Hash paketinin saklanan uzunluğu |
| `AccessFailedCount` | Identity'nin saydığı başarısız girişler |
| `LockoutEnd` | Hesap kilitlenmişse kilidin bitiş zamanı |

Sorgu parolayı veya hash'in tamamını göstermez. Parola geri okunamaz; doğru parola, giriş sırasında kayıtlı hash üzerinden doğrulanır.

Hazır sorgular [inspect-auth.sql](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/database/inspect-auth.sql) dosyasında da bulunur. Sorgular yalnızca okuma yapar. Parola değiştirmek veya hesap oluşturmak için Workbench'te elle `PasswordHash` yazma; bu işlemler Identity üzerinden yapılır.

## 5. Postman'de giriş akışını uygula

Postman'de **Import** ile [Afterform.Step6.postman_collection.json](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/postman/Afterform.Step6.postman_collection.json) dosyasını içeri al. Sürümünde **Use resources or import** görünüyorsa onun altındaki **Import** sekmesini aç; dosyayı içeri sürükleyebilirsin. [Postman import belgesi](https://learning.postman.com/docs/getting-started/importing-and-exporting/importing-data/).

Koleksiyonun değişkenlerinde:

| Değişken | Değer |
|---|---|
| `baseUrl` | `http://localhost:5000` |
| `userName` | Oluşturduğun kullanıcı adı; örneğin `mira.sol` |
| `password` | Kendi seçtiğin parola; dosyada boş bırakılmıştır |
| `csrfToken` | CSRF isteğinden otomatik alınır |

Parolayı yalnızca kendi Postman kullanımın için doldur. İşin bitince `password` değerini temizle. Parola doldurulmuş koleksiyonu paylaşma veya dışarı aktarma.

**Authorization: No Auth** kalır. Bu API oturum çerezi kullanır. Postman'in cookie jar'ı açık olmalı; isteğin **Settings** sekmesindeki **Disable cookie jar** kapalı kalmalı. Cookie başlığını elle yazman gerekmez. Tarayıcıdaki giriş ile Postman'deki giriş ayrı oturumlardır. [Postman çerez yönetimi](https://learning.postman.com/docs/use/send-requests/response-data/cookies/).

İstekleri aşağıdaki sırada, tek tek **Send** düğmesiyle gönder:

### 01 — Giriş öncesi CSRF token al

```http
GET {{baseUrl}}/api/auth/csrf
```

JSON yanıtta `requestToken` gelir. Koleksiyondaki kısa post-response betiği yalnızca bu değeri `csrfToken` değişkenine alır. Aynı yanıtla ilişkili çerezleri Postman cookie jar saklar.

### 02 — Sanatçı girişi

```http
POST {{baseUrl}}/api/auth/login
Content-Type: application/json
X-AF-CSRF: {{csrfToken}}
```

**Body → raw → JSON** alanında `{{afLoginBody}}` hazırdır. İstek gönderilmeden önce kullanıcı adı ve parola değişkenlerinden şu biçimde JSON üretilir:

```json
{
  "userName": "{{userName}}",
  "password": "{{password}}"
}
```

Koleksiyondaki hazır pre-request betiği, parolanda tırnak veya ters eğik çizgi varsa bunları doğru JSON biçiminde gönderir. Gövdeyi elle düzenlemen gerekmez. Üretilen gövde yalnızca geçici istek/run scope değişkeninde tutulur; koleksiyon değişkenlerine ayrıca yazılmaz. [Postman değişken kapsamları](https://learning.postman.com/docs/use/send-requests/variables/variables/#variable-scopes).

Başarılı giriş, sonraki isteklerin kullanacağı oturum çerezini oluşturur. Parolayı her istekte tekrar göndermezsin.

### 03 — Hangi hesapla giriş yaptığını öğren

```http
GET {{baseUrl}}/api/auth/me
```

Oturum varsa kendi hesap bilgilerin gelir:

```json
{
  "id": "hesabının-kimliği",
  "userName": "mira.sol",
  "displayName": "Mira Sol"
}
```

Giriş yapılmamışsa `401` gelir. `me`, giriş durumunu kontrol etmek için portalın da kullandığı uç noktadır.

### 04 — Giriş sonrası CSRF token'ı yenile

```http
GET {{baseUrl}}/api/auth/csrf
```

Girişle birlikte kullanıcı kimliği değiştiği için logout öncesinde yeni token al. Koleksiyon aynı `csrfToken` değişkenini günceller. Girişten önce aldığın token'ı logout için tekrar kullanma.

### 05 — Çıkış yap

```http
POST {{baseUrl}}/api/auth/logout
X-AF-CSRF: {{csrfToken}}
```

Başarılı çıkış `204 No Content` döndürür; yanıt gövdesinin boş olması normaldir. Daha sonra `03 — Hesabım` isteğini yeniden gönderirsen oturum olmadığı için `401` görürsün.

## 6. Hash, salt ve çerez hakkında bilmen gerekenler

**Parola saklama:** Bu projede IdentityV3 parola hasheri, PBKDF2-HMAC-SHA512, 220.000 yineleme ve her parola için rastgele 16 bayt salt kullanır. Salt, algoritma bilgisi ve hash, `PasswordHash` alanındaki paket içinde saklanır; ayrıca elle bir salt sütunu doldurmazsın. Algoritma ve paket yapısı [Identity'nin resmi kaynak kodunda](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/PasswordHasher.cs) görülebilir. Projede ayarladığımız 220.000 yineleme, [OWASP'ın PBKDF2-HMAC-SHA512 değeriyle](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2) eşleşir. Parola hasheri ve yineleme sayısı [Identity ayarlarıyla](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0) yönetilir.

Hash işlemi parolayı geri çözülebilir bir şifreye dönüştürmez. Salt, aynı parolayı seçen iki hesabın hash'lerinin aynı olmasını engeller; PBKDF2'nin maliyeti parola tahminlerini yavaşlatır. SHA-512, burada parola saklamaya uygun PBKDF2 yapısının içinde kullanılır. [OWASP parola saklama rehberi](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).

**Oturum:** Başarılı girişte kimliği temsil eden çerez verilir. Oturum 8 saat geçerlidir; aktif kullanımda sliding expiration ile yenilenebilir. Kalıcı çerez olarak yazılmaz; “beni hatırla” seçeneği yoktur.

**CSRF:** Tarayıcı oturum çerezini otomatik gönderdiği için veri değiştiren isteklere ek bir token gerekir. Login ve logout için `X-AF-CSRF` başlığındaki token ile ilişkili çerez birlikte doğrulanır. Bu token, oturum çerezinden ayrı görev yapar. [Microsoft CSRF belgesi](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

**Giriş sınırları:** Bir hesapta 5 yanlış parola denemesi 15 dakikalık hesap kilidi oluşturur. Ayrıca giriş uç noktasında IP başına dakikada 10 istek sınırı vardır. Bunlar ayrı kontrollerdir: ilki hesap, ikincisi isteğin geldiği IP içindir.

## 7. Takılırsan hangi yere bakmalısın?

| Gördüğün durum | Uygulayacağın kontrol |
|---|---|
| Postman `Could not send request` diyor | Backend PowerShell penceresi açık mı, `baseUrl` doğru mu? |
| `me` isteği `401` veriyor | Önce login gönder; cookie jar'ın açık olduğundan emin ol. |
| Login başarısız | MySQL kullanıcısı yerine sanatçı kullanıcı adını mı girdin? Parola doğru mu? |
| CSRF ile ilgili `400` geliyor | Aynı Postman oturumunda yeniden CSRF al; `X-AF-CSRF` başlığını kontrol et. |
| `429` geliyor | IP istek sınırına ulaştın; bir süre bekleyip tekrar dene. |
| Art arda yanlış girişten sonra doğru parola da kabul edilmiyor | Hesap kilidi süresini bekle; Workbench'te `LockoutEnd` alanını incele. |
| `/api/health/ready` bağlantı hatası gösteriyor | Docker MySQL konteyneri çalışıyor mu? |

İlk uygulaman: hesabı oluştur, tarayıcıda giriş yap, Workbench'te hash varlığını gör, Postman'de beş isteği sırayla gönder. Böylece hesap kaydı, giriş doğrulaması ve oturumun sonlanmasını ayrı ayrı gözlemlemiş olursun.
