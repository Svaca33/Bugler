using Bugler.Alerting.Episodes;

namespace Bugler.Alerting;

internal sealed class CzechAlertingMessages : AlertingMessages
{
    public override string ActionBelongsToNewestEpisode =>
        "Akce náleží nejnovější epizodě svého druhu.";

    public override string SolvedEpisodeNeverAcknowledged =>
        "Vyřešená epizoda se už nepřebírá.";

    public override string WithdrawingNeverRefuses => "Vrácení převzetí nikdy neodmítá.";

    public override string EpisodeAlreadySolved =>
        "Epizoda už je vyřešená; verdikt se vynáší jen jednou.";

    public override string QuietWindowAtLeastOneMinute =>
        "Tiché okno musí být alespoň 1 minuta.";

    public override string ClaimLeaseAtLeastOneHour =>
        "Lease strojního převzetí musí být alespoň 1 hodina.";

    public override string WebhookMustBeHttps => "Webhook musí být absolutní https URL.";

    public override string HealthCheckMustBeUrl =>
        "Health check musí být absolutní http nebo https URL.";

    public override string QuietWindowBetween(int maxMinutes) =>
        $"Tiché okno musí být mezi 1 a {maxMinutes} minutami.";

    public override string EpisodePredatesGrouping =>
        "Tato epizoda předchází seskupování podle druhu potíží a tiché okno nést nemůže.";

    public override string ApplicationOutsideVisibility =>
        "Aplikaci mimo vaši viditelnost nelze odebírat.";

    public override string UnregisteredService =>
        "Neregistrovanou službu nelze odebírat.";

    public override string ServiceOutsideVisibility =>
        "Službu mimo vaši viditelnost nelze odebírat.";

    public override AlertWords WordsFor(Watch watch) => watch switch
    {
        Watch.HealthCheck => new AlertWords(
            "Bez odpovědi od", "přestal odpovídat na svůj health check.", "Health check"),
        _ => new AlertWords("Potíže v", "začal logovat potíže.", "První log"),
    };

    public override AlertWords ResignationWords => new(
        "Stroj se vzdal v",
        "nese potíže, které stroj vzdal: podíval se a z kódu je opravit neumí. "
        + "Je potřeba lidská ruka.",
        "Důvod stroje");

    public override string NoBody => "(bez těla)";

    public override string EpisodeLinkLabel => "Epizoda";

    public override string OpenEpisodeButton => "Otevřít epizodu";

    public override string ReadingLabel => "Výklad AI";
}
