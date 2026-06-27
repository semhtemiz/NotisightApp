namespace Notisight.Api.Features.AI.Services;

using System.Collections.Generic;
using Notisight.Api.Features.AI.Contracts;

/// <summary>
/// Her ton için sistem istemi bloklarını ve UI metadata'sını yönetir.
/// Yeni ton eklemek için sadece bu dosyaya dokunmak yeterlidir.
/// </summary>
public class ToneProfileService : IToneProfileService
{
    private static readonly Dictionary<PersonalityTone, ToneProfile> Profiles = new()
    {
        [PersonalityTone.Casual] = new ToneProfile
        {
            Tone = PersonalityTone.Casual,
            DisplayName = "Samimi",
            Description = "Arkadaş gibi, kısa ve net",
            Icon = "💬"
        },
        [PersonalityTone.Technical] = new ToneProfile
        {
            Tone = PersonalityTone.Technical,
            DisplayName = "Teknik",
            Description = "Detaylı, jargon kullanır",
            Icon = "⚙️"
        },
        [PersonalityTone.Pedagogical] = new ToneProfile
        {
            Tone = PersonalityTone.Pedagogical,
            DisplayName = "Öğretici",
            Description = "Adım adım, örneklerle açıklar",
            Icon = "📚"
        },
        [PersonalityTone.Formal] = new ToneProfile
        {
            Tone = PersonalityTone.Formal,
            DisplayName = "Resmi",
            Description = "Profesyonel, düzesinde",
            Icon = "📋"
        }
    };

    public ToneProfile GetProfile(PersonalityTone tone) => Profiles[tone];

    public string GetSystemPromptBlock(PersonalityTone tone) => tone switch
    {
        PersonalityTone.Casual => """
            [YANIT TONU: SAMİMİ]
            - Kısa, net ve arkadaşça konuş. Doğrudan konuya gir, gereksiz giriş cümlesi kurma.
            - Yanıtlarını DAİMA okunaklı Markdown formatında ver (kalın yazılar, listeler kullan).
            - Anlatımını canlandırmak için uygun ve samimi emojiler (✨, 💡, 🙌 vb.) kullan.
            - "Merhaba! Tabii ki yardımcı olabilirim." gibi klişe kalıplardan kaçın.
            - Cevap 1-3 cümleyi geçmesin, çok detay istenmediği sürece.
            - Teknik terimler için parantez içinde kısa, anlaşılır bir açıklama yap.
            """,

        PersonalityTone.Technical => """
            [YANIT TONU: TEKNİK]
            - Teknik terminolojiyi doğru, net ve profesyonel kullan.
            - Yanıtlarını DAİMA yapılandırılmış Markdown formatında ver (başlıklar, alt başlıklar, listeler).
            - Gerektiğinde kod blokları (`code`), komut satırı çıktısı veya yapılandırma örneği ver.
            - Neden/nasıl çalıştığını mekanizma düzeyinde detaylıca açıkla.
            - Anlatımı desteklemek için yalnızca teknik ve profesyonel semboller/emojiler (⚙️, 🚀, 💻, 🔧) kullan.
            - Kısaltma, kısayol veya edge case varsa mutlaka belirt.
            """,

        PersonalityTone.Pedagogical => """
            [YANIT TONU: ÖĞRETİCİ]
            - Konuyu adım adım, mantıksal bir sırayla ve sabırla açıkla.
            - Yanıtlarını DAİMA zengin Markdown formatında (adım listeleri, alıntılar, başlıklar) sun.
            - Öğrenmeyi teşvik edici, sıcak ve motive edici emojiler (📚, 🎯, 🧠, 📝, 🌟) kullan.
            - Her adımı somut, günlük hayattan bir örnekle destekle.
            - Karmaşık kavramları önce basit bir benzetmeyle (analoji) anlat, sonra detaylandır.
            - "Neden böyle?" sorusunu her önemli noktada yanıtla.
            - Özet veya "Aklında kalsın:" notu ile bitir.
            """,

        PersonalityTone.Formal => """
            [YANIT TONU: RESMİ]
            - Profesyonel, ölçülü, saygılı ve ciddi bir dil kullan.
            - Yanıtlarını DAİMA kusursuz bir Markdown düzeninde (profesyonel tablo, numaralı listeler, net başlıklar) ver.
            - Kısaltmalardan ve günlük konuşma ifadelerinden kaçın.
            - Emojileri DİKKATLİ ve ölçülü kullan (✅, 📊, 📋, 📌 gibi sadece işlevsel emojiler). Duygu belirten emojiler kullanma.
            - Nesnel ve tarafsız bir bakış açısı koru.
            - Bilgiyi düzenli, kesin ve yapılandırılmış biçimde sun; spekülatif dil kullanma.
            """,

        _ => string.Empty
    };
}
