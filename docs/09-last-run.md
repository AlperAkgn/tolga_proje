# AFTER/FORM — 9. adım çalıştırma sonucu

- Durum: **passed**
- Başlangıç (UTC): 2026-10-02T17:50:26.238Z
- Yerel test adresi: http://127.0.0.1:5019
- Postman senaryolarındaki HTTP isteği: 120
- Kontrol: 197
- Başarısızlık: 0
- Kurulum hatası: Yok

## Senaryolar

| Grup | İstek | Kontrol | Başarısız |
|---|---:|---:|---:|
| 01 — Sessions and ownership | 63 | 110 | 0 |
| 02 — Upload validation | 30 | 46 | 0 |
| 03 — Account lockout | 15 | 27 | 0 |
| 04 — Login rate limit | 12 | 14 | 0 |

## Deneme verileri

- Kullanıcı: qa9.a.20261002.6e2d3043079c
- Kullanıcı: qa9.b.20261002.6e2d3043079c
- Gönderim kimlikleri: 4, 5, 6

Bu rapor parola, oturum çerezi, CSRF token, istek gövdesi veya tam Newman çıktısı içermez.
Oluşturulmuş QA hesapları ve başarılı yapay demo kayıtları incelemek için tutulur. B hesabı kilitlenme grubu başarılıysa 15 dakika kilitlidir.

Bu senaryoların geçmesi bütün güvenlik açıklarının bulunmadığı anlamına gelmez. Üretim HTTPS, çok sunuculu rate limit ve tam dosya/zararlı yazılım analizi bu koşuda doğrulanmaz.
