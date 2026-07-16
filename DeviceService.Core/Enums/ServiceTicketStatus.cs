namespace DeviceService.Core.Enums;

public enum ServiceTicketStatus
{
    TeslimAlindi = 0,      // Teslim Alındı
    İncelemede = 1,         // İncelemede
    ParcaBeklenıyor = 2,    // Parça Bekleniyor
    TamirEdiliyor = 3,      // Tamir Ediliyor
    TestEdiliyor = 4,       // Test Ediliyor
    Hazir = 5,              // Hazır
    TeslimEdildi = 6,       // Teslim Edildi
    İptal = 7               // İptal
}
