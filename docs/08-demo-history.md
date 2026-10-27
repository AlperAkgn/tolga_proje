# 8. adım — Gerçek demo geçmişi ve durum takibi

**Durum: tamamlandı.** Backend `DemoHistory` yapılandırmasında **0 hata, 0 uyarı** ile derlendi; JavaScript sözdizimi kontrolleri başarılı. Kullanıcı sunucunun açıldığını bildirdi. Daha sonra 9. adımın gerçek Newman koşusunda MySQL liste/ayrıntı, arama, durum filtresi, sayfa doğrulaması ve karşılıklı sanatçı sahipliği kontrolleri geçti. 9. adımın dört grubu toplam 120 istek / 197 kontrol / 0 başarısızlıkla tamamlandı. Tarayıcıdaki görsel/etkileşim akışı bu API koşusunda denenmedi.

Bu adımda tablo yapısı değişmediği için **yeni migration yok**. 7. adımın `DemoFileIntegrity` migration'ının uygulanmış olması gerekir. A&R paneli veya sanatçının durum değiştirebildiği bir API eklenmedi.

## 1. Güncel sürümü aç

Docker'daki mevcut MySQL konteyneri açık kalsın. Backend'in çalıştığı normal PowerShell penceresinde:

1. `Ctrl+C` ile sunucuyu durdur.
2. Şu komutu çalıştır:

```powershell
& "C:\Users\Windows11\Documents\Codex\2026-09-27\sen\scripts\Start-Backend.ps1"
```

`Now listening on` göründüğünde [sanatçı portalını](http://localhost:5000/artist/) aç ve `Ctrl+F5` ile yenile. Bu terminal açık kalmalı. Hesabı yeniden oluşturman veya veritabanı hazırlama betiğini tekrar çalıştırman gerekmez.

Sunucu normal kullanıcı terminalinde yönetiliyor: bu çalışma ortamının .NET–MySQL bağlantısı önceki adımlarda Windows TLS/SSPI kısıtına takıldı. Bu yüzden burada derlenen kodun çalışan sunucuda etkinleşmesi kullanıcı terminalindeki yeniden başlatmayla yapılır.

## 2. Portalda ne değişti?

Önceki üç örnek şarkı kaldırıldı. `/artist/` sayfası artık giriş yapan sanatçının MySQL kayıtlarını ister.

- Hesabın hiç demo göndermediyse gerçek boş durum ve **Send your first demo** bağlantısı gösterilir.
- Gönderim varsa başlık, tür, dosya boyutu, gönderim tarihi ve güncel durum listelenir.
- **All demos / Submitted / In review / Accepted / Rejected** düğmeleri listeyi filtreler.
- **Find a track** alanı şarkı adında veya türünde arar. Yazmayı kısa süre bıraktığında istek gönderilir; Enter ile de arayabilirsin.
- Her sayfada 10 kayıt bulunur; en yeni gönderim önce gelir.
- **View demo** veya şarkı adına tıklayınca `/artist/demo/?id=...` ayrıntı sayfası açılır.
- Ayrıntıda sanatçı notu, dosya adı, gönderim ve güncelleme zamanı, dinleme ve orijinali indirme seçenekleri bulunur.
- **Refresh** düğmesi veritabanındaki bilgiyi yeniden ister. Açık ve görünür sayfada yaklaşık dakikada bir yenileme kontrolü de yapılır. Durum yenilenirken çalan kaydın ses kaynağı değiştirilmez.

Üstteki beş sayaç hesabının **bütün kayıtlarını** sayar. `Demo history / ...` başlığındaki sayı ise seçtiğin durum ve aramayla eşleşen kayıt sayısıdır. Örneğin hesabında 12 demo varsa All demos 12 kalır; Electronic aramasında yalnızca 3 eşleşiyorsa liste başlığı 3 gösterir.

Tarihler veritabanında UTC tutulur ve API yanıtında UTC olarak işaretlenir. Tarayıcı bunları senin yerel saatine çevirir. Güncelleme zamanı yoksa hayali bir inceleme tarihi üretilmez.

## 3. Durumların anlamı

| Veritabanı/API değeri | Ekran | Anlam |
|---|---|---|
| `Submitted` | Submitted | Kayıt sisteme alındı, inceleme bekliyor |
| `InReview` | In review | Label kaydı inceliyor |
| `Accepted` | Accepted | Label gönderimi kabul etti |
| `Rejected` | Rejected | Label gönderimi reddetti |

Gerçek yüklemeler başlangıçta `Submitted` olur. Şu an A&R iş akışını geliştirmediğimiz için gönderimler kendiliğinden kabul veya ret durumuna geçmez. Arayüz mevcut veritabanı değerini okur; bir süre beklemek durumu değiştirmez. Sanatçıya durum düzenleme yetkisi verilmedi.

Durum ile `UpdatedAtUtc` ileride A&R tarafındaki işlemde birlikte güncellenmelidir. Bu sürümde karar gerekçesi veya inceleme geçmişi alanı da bulunmuyor. Ayrıntı sayfasındaki not, sanatçının yüklerken yazdığı nottur.

## 4. Bir kaydın izlediği yol

1. Sanatçı giriş yapar; tarayıcı oturum çerezini saklar.
2. Gönderim formu gerçek dosyayı `POST /api/demos` ile gönderir.
3. 7. adımın servisi dosyayı özel klasöre, bilgileri MySQL'e kaydeder ve `201` döndürür.
4. Sanatçı **View all my demos** ile listeye gider.
5. Liste `GET /api/demos` ile sunucudan kayıtları alır.
6. Ayrıntı `GET /api/demos/{id}` ile tek kaydı alır; ses ayrı, korumalı uç noktadan okunur.

Mevcut `/artist/submit/?demo=...` makbuz bağlantıları da çalışmaya devam eder. Makbuzdaki **View demo details**, yeni ayrıntı sayfasını açar.

Veritabanı veya bağlantı hatasında sahte bir boş liste gösterilmez. Hata ve tekrar deneme seçeneği görünür. Arka plan yenilemesi başarısızsa önceki kayıtlar tutulabilir; bunların son başarılı yenilemeye ait olduğu açıkça belirtilir.

## 5. MySQL Workbench ile kayıtları oku

[inspect-demo-history.sql](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/database/inspect-demo-history.sql) dosyasını Workbench'te bir SQL sekmesinde aç. `mira.sol` yerine kendi kullanıcı adını yazabilirsin. İçinde yalnızca okuma sorguları var:

- Sanatçının bütün gönderimleri ve gerçek durumları.
- Portalın üst sayaçlarına karşılık gelen durum sayıları.
- Tür/başlık araması ve durum filtresi örneği.
- Tek bir gönderimin ayrıntısı.

Henüz gerçek dosya göndermediysen sorguların boş dönmesi normaldir. Bu adım örnek demo kayıtları eklemez. Workbench'teki yetkili veritabanı bağlantısı ile sanatçının API erişimi farklıdır; sanatçı sınırı API'de oturum kimliğiyle uygulanır.

## 6. Postman'de aynı veriyi oku

[Afterform.Step8.postman_collection.json](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/postman/Afterform.Step8.postman_collection.json) dosyasını **Import** ile içeri al.

1. Koleksiyon Variables bölümünde `baseUrl=http://localhost:5000`, `userName=kendi sanatçı adın`, `password=kendi parolan` ayarla. Parola dosyada boş bırakıldı.
2. Authorization **No Auth**, cookie jar açık olsun. Tarayıcı ve Postman ayrı oturum kullanır.
3. **01 CSRF**, ardından **02 Sanatçı girişi** isteklerini gönder.
4. **03 Oturumdaki sanatçı** ile hesabını gör.
5. **04 Demo geçmişi** ile kendi kayıtlarını listele. `items` boşsa henüz gönderimin yoktur; gerçek yükleme için 7. adım koleksiyonunu veya siteyi kullanabilirsin.
6. **05 Durum filtresi** için `status` değişkenini dört durumdan birine ayarla. O durumda kayıt yoksa `items: []` normaldir.
7. **06 Arama** için `searchTerm` alanını değiştir. Params bölümünden durum ile aramayı aynı istekte de birleştirebilirsin.
8. Liste yanıtındaki bir kaydın `id` değerini `demoId` değişkenine **kendin kopyala**. **07 Ayrıntı** ve **08 Orijinali indir** bunu kullanır. İndirmede Send and Download / Save Response seçeneğini kullanabilirsin.
9. Çıkış için **09 CSRF**, sonra **10 Çıkış** isteğini gönder.

Koleksiyondaki kısa CSRF betikleri yalnızca token'ı aktarır; otomatik test assertion'ı eklenmedi. Koleksiyon çalıştırılmadı. Parolayı kullandıktan sonra temizle; parola doldurulmuş koleksiyonu dışa aktarma/paylaşma.

### Liste API'si

```text
GET /api/demos?page=1&pageSize=10&status=Submitted&search=Electronic
```

`status` ve `search` isteğe bağlı. Sayfa numarası 1–1.000.000, sayfa boyutu 1–50, arama metni en fazla 100 karakter. Geçersiz değer `400` verir. Kayıt sayısından büyük bir sayfa istenirse son mevcut sayfa döner; kayıt yoksa `page=1`, `totalPages=0` döner.

| Yanıt alanı | Görev |
|---|---|
| `artistId` | Bu yanıtın ait olduğu oturum hesabı |
| `items` | İstenen sayfadaki kendi demo kayıtları |
| `page`, `pageSize` | Gerçekte kullanılan sayfa/boyut |
| `totalCount`, `totalPages` | Filtrelenmiş sonuç sayısı ve sayfa sayısı |
| `summary` | Hesabın filtre uygulanmadan hesaplanan durum sayaçları |

Oturum yoksa `401`; MySQL erişilemezse `503` beklenir. Tek kayıt veya ses için başka sanatçıya ait kimlik ile olmayan kayıt aynı `404` yanıtını verir. Geniş erişim ve hata senaryoları 9. adımın Postman koleksiyonuyla kontrol edildi; sonuç `docs/09-last-run.md` dosyasında.

## 7. Tasarım tercihleri

- **Sahiplik ilk filtre:** Kullanıcı kimliği oturumdan alınır; arama, sayaç ve sayfalama bundan sonra uygulanır. URL'den sanatçı kimliği seçilmez. Ayrıntı ve ses uç noktaları da ayrı ayrı sahipliği denetler.
- **Salt okunur sorgu:** EF Core `AsNoTracking` kullanır; listeleme kayıt değiştirmez. [Microsoft açıklaması](https://learn.microsoft.com/en-us/ef/core/querying/tracking).
- **Sınırlı sonuç:** `Skip/Take` ile bütün demolar tek yanıtta yüklenmez. Sıralama tarih ve benzersiz kimlik üzerindedir; aynı tarihli gönderimler belirli sırada kalır. Yoğun eşzamanlı eklemelerde sayfa içeriği kayabilir; bu küçük portal için başlangıç tercihi offset sayfalama. [EF Core sayfalama rehberi](https://learn.microsoft.com/en-us/ef/core/querying/pagination).
- **Güvenli gösterim:** Başlık ve not gibi kullanıcı metinleri `textContent` ile yerleştirilir. Dosya bağlantısı doğrulanmış sayısal kayıt kimliğinden üretilir; özel depolama yolu istemciye gönderilmez.
- **İstek yarışları:** Filtre değiştiğinde eski liste isteği iptal edilir. Hesap değişiminde eski veri temizlenir; yanıtın hesap kimliği de ön yüzde kontrol edilir. Bu kontrol ek tutarlılık sağlar; yetkilendirmenin asıl yeri sunucudur.
- **Özel verinin ömrü:** Liste ve ayrıntı tarayıcı depolamasına yazılmaz. Sayfadan ayrılırken veri temizlenir; geri dönüşte oturum ve kayıtlar yeniden istenir. Sunucu özel sayfa/API için `no-store` uygular.

Backend derlemesi ve JavaScript sözdizimi kontrolleri başarılı. Gerçek MySQL sağlayıcısındaki API sorguları ve sanatçı erişim sınırları ayrıca 9. adımda kontrol edildi. Tarayıcıdaki görsel akış ve ses oynatması bu koşuda doğrulanmadı.
