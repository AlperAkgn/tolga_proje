# AFTER/FORM — 10. adım Docker başlatma sonucu

**Sonuç: başarılı.** Kullanıcı normal PowerShell'de başlatma betiğini çalıştırdı. Terminal çıktısı ve betiğin oluşturduğu `.local/docker/last-start.json` raporu okunarak aşağıdaki sonuçlar doğrulandı.

| Kontrol | Sonuç |
|---|---|
| Docker imajı | `afterform-api:local` oluşturuldu |
| Backend konteyneri | `afterform-backend-1`, `healthy` |
| Başlatma raporu | `ready`, son aşama 6/6 |
| Site adresi | http://localhost:8080/ |
| Gerçek MySQL bağlantısı | `connected` |
| Migration durumu | `upToDate` |
| Mevcut MySQL konteyneri | `688c9576f123` |
| MySQL kalıcı depolaması | `mysql_data` volume'u → `/var/lib/mysql` |
| Ortak Docker ağı | `afterform-local` |
| Başlangıç (UTC) | 2026-10-02T18:17:21.9983093Z |
| Bitiş (UTC) | 2026-10-02T18:19:04.4439166Z |

## Backend'in kalıcı klasörleri

- Demo dosyaları: `C:\Users\Windows11\Documents\Codex\2026-09-27\sen\.local\uploads\demos` → `/var/afterform/demos`.
- Linux oturum anahtarları: `C:\Users\Windows11\Documents\Codex\2026-09-27\sen\.local\docker\data-protection` → `/var/afterform/keys`.

Bu klasörler Compose ile bind mount olarak bağlanır. Veritabanı mevcut MySQL volume'unda kalır. İlk Docker geçişinde ayrı Linux anahtarları kullanıldığından yeniden giriş gerekir.

## Sonucun kapsamı

Gerçek imaj oluşturma, healthy başlatma, veritabanı bağlantısı, güncel şema ve MySQL'in kalıcı mount bilgisi doğrulandı. Docker'da yeni demo yükleyip konteyneri yeniden oluşturma, tarayıcıda ses oynatma ve 9. adımın güvenlik koleksiyonunu Linux konteynerinde yeniden çalıştırma bu başlatma koşusuna dahil değildi.

Bu kurulum yerel HTTP geliştirme ortamıdır; Azure'a yayın yapılmadı. Günlük komutlar ve kalıcılığı gözlemleme adımları [Docker rehberinde](C:/Users/Windows11/Documents/Codex/2026-09-27/sen/docs/10-docker-backend.md) bulunur. Raporda parola, bağlantı sırrı veya oturum çerezi yoktur.
