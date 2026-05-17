using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowFrontend");

BiletYonetimSistemi sistem = new BiletYonetimSistemi();
sistem.VerileriYukle();
sistem.KullanicilariYukle();


app.MapGet("/api/yarislar", () => Results.Ok(sistem.YarisListesiGetir()));

app.MapPost("/api/yarislar", (Yaris yeniYaris) => {
    sistem.YarisEkle(yeniYaris);
    return Results.StatusCode(StatusCodes.Status201Created);
});

app.MapPost("/api/bilet-al", (BiletIstegi istek) => {
    try
    {
        Bilet bilet = sistem.BiletSatinAl(istek.YarisId, istek.MusteriAdi ?? "Bilinmeyen Müşteri");
        return Results.Ok(new { mesaj = "Başarılı", biletKodu = bilet.BiletKodu, yaris = bilet.SecilenYaris.Ad });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.MapDelete("/api/yarislar/{id:int}", (int id) => {
    sistem.YarisSil(id);
    return Results.Ok(new { mesaj = "Yarış sistemden kaldırıldı." });
});

app.MapPost("/api/kayit-ol", (KayitIstegi istek) => {
    try
    {
        sistem.KullaniciKaydet(istek.KullaniciAdi, istek.Eposta, istek.Sifre);
        return Results.Ok(new { mesaj = "Kayıt başarıyla tamamlandı!" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.MapPost("/api/giris-yap", (GirisIstegi istek) => {
    try
    {
        YarisseverMusteri musteri = sistem.KullaniciGirisKontrol(istek.KullaniciAdi, istek.Sifre);
        return Results.Ok(new { mesaj = musteri.GirisMesaji(), kullaniciAdi = musteri.KullaniciAdi });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.Run();



public abstract class Kullanici
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Eposta { get; set; } = string.Empty;
    public string Sifre { get; set; } = string.Empty;

    public Kullanici(string kullaniciAdi, string eposta, string sifre)
    {
        KullaniciAdi = kullaniciAdi;
        Eposta = eposta;
        Sifre = sifre;
    }

    public virtual string GirisMesaji() => $"Kullanıcı Girişi: {KullaniciAdi}";
}

public class YarisseverMusteri : Kullanici
{
    public YarisseverMusteri(string kullaniciAdi, string eposta, string sifre) 
        : base(kullaniciAdi, eposta, sifre) 
    { 
    }
    
    public override string GirisMesaji() => $"Hoş geldiniz Yarışsever {KullaniciAdi}!";
}

public class Yaris
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Tarih { get; set; } = string.Empty;
    public string Resim { get; set; } = string.Empty; 

    private double _fiyat;
    public double Fiyat { get => _fiyat; set => _fiyat = value < 0 ? 0 : value; }

    private int _kapasite;
    public int Kapasite { get => _kapasite; set => _kapasite = value < 0 ? 0 : value; }

    public Yaris(int id, string ad, string tarih, double fiyat, int kapasite, string resim)
    {
        Id = id; Ad = ad; Tarih = tarih; Fiyat = fiyat; Kapasite = kapasite; Resim = resim;
    }
}

public class Bilet
{
    public string BiletKodu { get; set; }
    public Yaris SecilenYaris { get; set; }
    public string MusteriAdi { get; set; }

    public Bilet(Yaris yaris, string musteriAdi)
    {
        BiletKodu = "F1-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        SecilenYaris = yaris;
        MusteriAdi = musteriAdi;
    }
}

public class BiletIstegi { public int YarisId { get; set; } public string? MusteriAdi { get; set; } }
public class KayitIstegi { public string KullaniciAdi { get; set; } = string.Empty; public string Eposta { get; set; } = string.Empty; public string Sifre { get; set; } = string.Empty; }
public class GirisIstegi { public string KullaniciAdi { get; set; } = string.Empty; public string Sifre { get; set; } = string.Empty; }


public class BiletYonetimSistemi
{
    private List<Yaris> yarislar = new List<Yaris>();
    private List<YarisseverMusteri> kullanicilar = new List<YarisseverMusteri>();
    
    private const string DosyaYolu = "yarislar.json";
    private const string KullaniciDosyaYolu = "kullanicilar.json";

    public List<Yaris> YarisListesiGetir() => yarislar;

    public void KullanicilariYukle()
    {
        if (File.Exists(KullaniciDosyaYolu))
        {
            string json = File.ReadAllText(KullaniciDosyaYolu);
            kullanicilar = JsonSerializer.Deserialize<List<YarisseverMusteri>>(json) ?? new List<YarisseverMusteri>();
        }
    }

    private void KullanicilariKaydet()
    {
        string json = JsonSerializer.Serialize(kullanicilar, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(KullaniciDosyaYolu, json);
    }

    public void KullaniciKaydet(string kAdi, string eposta, string sifre)
    {
        if (kullanicilar.Exists(u => u.KullaniciAdi.ToLower() == kAdi.ToLower()))
            throw new Exception("Bu kullanıcı adı zaten alınmış!");
        
        if (kullanicilar.Exists(u => u.Eposta.ToLower() == eposta.ToLower()))
            throw new Exception("Bu e-posta adresiyle zaten kayıt olunmuş!");

        kullanicilar.Add(new YarisseverMusteri(kAdi, eposta, sifre));
        KullanicilariKaydet();
    }

    public YarisseverMusteri KullaniciGirisKontrol(string kAdi, string sifre)
    {
        YarisseverMusteri? m = kullanicilar.Find(u => u.KullaniciAdi.ToLower() == kAdi.ToLower() && u.Sifre == sifre);
        if (m == null) throw new Exception("Kullanıcı adı veya şifre hatalı!");
        return m;
    }

    public void VerileriYukle()
    {
        if (File.Exists(DosyaYolu))
        {
            string json = File.ReadAllText(DosyaYolu);
            yarislar = JsonSerializer.Deserialize<List<Yaris>>(json) ?? new List<Yaris>();
        }
        else
        {
            yarislar.Add(new Yaris(1, "Kanada GP", "22-24 MAYIS", 450, 150, "kanada.jpeg"));
            yarislar.Add(new Yaris(2, "Monako GP", "05-07 HAZİRAN", 550, 120, "monako.jpeg"));
            yarislar.Add(new Yaris(3, "İspanya GP", "12-14 HAZİRAN", 600, 90, "ispanya.jpeg"));
            VerileriKaydet();
        }
    }

    private void VerileriKaydet()
    {
        string json = JsonSerializer.Serialize(yarislar, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DosyaYolu, json);
    }

    public void YarisEkle(Yaris yeniYaris) { yarislar.Add(yeniYaris); VerileriKaydet(); }

    public Bilet BiletSatinAl(int yarisId, string musteriAdi)
    {
        Yaris? yaris = yarislar.Find(y => y.Id == yarisId);
        if (yaris == null) throw new Exception("HATA: Seçilen yarış takvimde bulunamadı!");
        if (yaris.Kapasite <= 0) throw new Exception("HATA: Bu yarış için tüm biletler tükendi!");

        yaris.Kapasite--; 
        VerileriKaydet(); 
        return new Bilet(yaris, musteriAdi);
    }

    public void YarisSil(int yarisId) { yarislar.RemoveAll(y => y.Id == yarisId); VerileriKaydet(); }
}