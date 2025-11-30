# Spor Salonu Yönetim Sistemi

ASP.NET Core MVC 8.0 ile geliştirilmiş, eğitim amaçlı bir spor salonu yönetim sistemidir.

## Proje Mantığı

Sistem **rol tabanlı yetkilendirme** ile çalışır:
- **Admin**: Spor salonu, antrenör ve hizmet yönetimi yapabilir. Tüm randevuları görüntüleyip onaylayabilir/reddedebilir.
- **Üye (Member)**: Randevu oluşturabilir, kendi randevularını görüntüleyip iptal edebilir. Yapay zeka destekli egzersiz ve diyet önerileri alabilir.

## Özellikler

- ✅ Rol tabanlı yetkilendirme (Admin/Üye)
- ✅ Spor salonu, antrenör ve hizmet yönetimi
- ✅ Randevu sistemi (oluşturma, onaylama, reddetme)
- ✅ OpenAI API entegrasyonu ile AI önerileri
- ✅ Türkçe arayüz
- ✅ SQLite veritabanı

## Kurulum

```bash
dotnet restore
dotnet run
```

## Varsayılan Admin Bilgileri

- **E-posta:** `ogrencinumarasi@sakarya.edu.tr`
- **Şifre:** `sau`

Admin kullanıcısı uygulama ilk çalıştırıldığında otomatik oluşturulur.

## Teknolojiler

.NET 8.0, ASP.NET Core MVC, Entity Framework Core, SQLite, ASP.NET Core Identity, OpenAI API
