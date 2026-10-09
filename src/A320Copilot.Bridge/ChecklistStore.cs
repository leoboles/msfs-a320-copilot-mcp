using System.Text.Json;

namespace A320Copilot.Bridge;

public sealed record ChecklistItem(string Id, string Instruction, string? TelemetryField = null, double? ExpectedValue = null);
public sealed record ChecklistTemplate(string Id, string Version, string Scope, IReadOnlyList<ChecklistItem> Items);
public sealed record ChecklistEvidence(string Source, DateTimeOffset ConfirmedAtUtc, string Note,
    DateTimeOffset? SampleAtUtc = null, string? MonitorId = null, long? TelemetrySessionId = null, string? Field = null, double? Value = null);
public sealed record ChecklistEntry(string ItemId, string Status, ChecklistEvidence? Evidence);
public sealed record ChecklistSession(string Id, string FlightLabel, ChecklistTemplate Template,
    int Revision, DateTimeOffset StartedAtUtc, IReadOnlyList<ChecklistEntry> Entries);
public sealed record ChecklistView(ChecklistSession Session, ChecklistItem? NextItem, bool AllItemsConfirmed,
    string Limitation = "Historical confirmations for this checklist only; not aircraft readiness, current telemetry, ATC clearance or an aircraft control command.");

/// <summary>Versioned simulator training checklists; progress survives MCP restarts.
/// A file lock and optimistic revision protect concurrent clients. Writes use atomic replacement.</summary>
public sealed class ChecklistStore(string directory)
{
    public static IReadOnlyList<ChecklistTemplate> Templates { get; } = Array.AsReadOnly(new[]
    {
        T("preparation", "Preparação inicial; testes de fogo antes da APU por preferência do usuário.",
            I("parking_brake", "Freio de estacionamento acionado.", "Controls.ParkingBrakeLever", 1),
            I("initial_controls", "Manetes IDLE, masters OFF, ENG MODE NORM; gear DOWN, flaps UP, speedbrake recolhido/desarmado e radar OFF: conferir cockpit."),
            I("electrical_supply", "Estabelecer alimentação elétrica conforme disponível; conferir a alimentação efetiva no ECAM ELEC."),
            I("fws_ready", "Aguardar inicialização do sistema de avisos antes dos testes de fogo."),
            I("apu_fire_test", "Executar teste de fogo APU e confirmar indicações visuais e sonoras esperadas."),
            I("engine1_fire_test", "Executar teste de fogo ENG 1 e confirmar indicações visuais e sonoras esperadas."),
            I("engine2_fire_test", "Executar teste de fogo ENG 2 e confirmar indicações visuais e sonoras esperadas."),
            I("apu_available", "Iniciar APU conforme procedimento e confirmar AVAIL.", "Overhead.ApuAvailable", 1),
            I("cockpit_preparation", "Concluir preparação FBW aplicável: ADIRS, oxigênio, sinais, iluminação, combustível e planejamento/performance; conferir itens restantes no guia.")),
        T("before_start", "Verificações antes da partida; não concede autorização de movimento.",
            I("thrust_idle", "Confirmar as duas manetes em IDLE."),
            I("fuel_left1", "L TK PUMP 1 selecionada ON.", "Fuel.Left1Switch", 1),
            I("fuel_left2", "L TK PUMP 2 selecionada ON.", "Fuel.Left2Switch", 1),
            I("fuel_right1", "R TK PUMP 1 selecionada ON.", "Fuel.Right1Switch", 1),
            I("fuel_right2", "R TK PUMP 2 selecionada ON.", "Fuel.Right2Switch", 1),
            I("fuel_center1", "CTR TK comando 1 ON/AUTO; é válvula de transferência no neo, não prova de fluxo.", "Fuel.Center1Switch", 1),
            I("fuel_center2", "CTR TK comando 2 ON/AUTO; é válvula de transferência no neo, não prova de fluxo.", "Fuel.Center2Switch", 1),
            I("starting_supply", "Confirmar APU disponível/bleed ou alimentação de partida aplicável e configuração pneumática correspondente."),
            I("beacon", "BEACON ON.", "Controls.BeaconOn", 1),
            I("cabin_ground", "Confirmar cabine/portas e equipamentos de solo liberados para a operação pretendida."),
            I("clearance", "Confirmar coordenação/autorização aplicável; autorização IFR não é autorização de pushback/táxi.")),
        T("pushback", "Somente coordenação de pushback; adaptar ao estado real dos motores e serviço de solo.",
            I("doors_equipment", "Confirmar portas fechadas e equipamentos/cabo externo retirados; EXT PWR OFF sozinho não comprova retirada."),
            I("tug_ready", "Confirmar caminhão conectado, equipe pronta e autorização/coordenação para pushback."),
            I("brake_release_request", "Aguardar solicitação do serviço de solo para liberar o freio."),
            I("brake_released", "Liberar freio quando solicitado.", "Controls.ParkingBrakeLever", 0),
            I("pushback_complete", "Confirmar término do pushback pelo serviço de solo."),
            I("brake_set", "Acionar freio quando solicitado, antes de desconectar o caminhão.", "Controls.ParkingBrakeLever", 1),
            I("tug_clear", "Confirmar caminhão desconectado e equipe/equipamentos afastados.")),
        T("engine_start", "Partida automática normal; ordem ENG 1 primeiro conforme guia FBW atual, adaptar ao SOP/estado existente.",
            I("prerequisites", "Confirmar before-start concluído, área/solo liberados, manetes IDLE e alimentação de partida adequada."),
            I("ignition", "ENG MODE IGN/START.", "Controls.Engine1IgnitionMode", 2),
            I("engine1_master", "ENG 1 MASTER ON inicia sequência automática FADEC; não há comando manual de injeção em N2 20%.", "Controls.Engine1MasterOn", 1),
            I("engine1_stable", "Monitorar partida ENG 1 no ECAM superior: parâmetros estáveis e sem avisos de falha; confirmação visual."),
            I("engine2_master", "ENG 2 MASTER ON inicia sequência automática FADEC.", "Controls.Engine2MasterOn", 1),
            I("engine2_stable", "Monitorar partida ENG 2 no ECAM superior: parâmetros estáveis e sem avisos de falha; confirmação visual.")),
        T("after_start", "Conferências após partida; flaps/trim dependem do planejamento e performance reais.",
            I("mode_norm", "ENG MODE NORM.", "Controls.Engine1IgnitionMode", 1),
            I("packs", "Conferir os dois packs ON/configuração pneumática aplicável no cockpit."),
            I("apu_bleed_off", "APU BLEED OFF conforme sequência aplicável.", "Overhead.ApuBleedOn", 0),
            I("apu_master_off", "APU MASTER OFF quando dispensável; não significa parada imediata.", "Overhead.ApuMasterOn", 0),
            I("anti_ice", "Anti-ice conforme condições."),
            I("configuration", "Conferir flaps de decolagem, ground spoilers, pitch trim conforme GW/CG e rudder trim zero."),
            I("flight_controls", "Conferir comandos de voo e indicações na página F/CTL; concluir after-start aplicável.")),
        T("before_taxi", "Verificações de táxi; nenhuma conclusão constitui autorização ATC ou prontidão integral.",
            I("after_start", "Confirmar after-start aplicável concluído e caminhão/equipe afastados."),
            I("atc_settings", "Conferir transponder, ALT RPTG e TCAS conforme fase/serviço ATC; não assumir STBY para táxi."),
            I("turnoff_lights", "RWY TURN OFF lights ON: conferir ambos os lados no cockpit."),
            I("nose_taxi", "No overhead EXT LT, NOSE em TAXI quando pronto para mover.", "Controls.NoseLightSelector", 1),
            I("taxi_clearance", "Confirmar autorização de táxi e trajeto; não confundir com autorização IFR."),
            I("area_clear", "Conferir área livre à esquerda/direita e equipe afastada."),
            I("brake_release", "Liberar freio para táxi somente com pré-requisitos satisfeitos.", "Controls.ParkingBrakeLever", 0),
            I("brake_check", "Executar verificação dos freios no início do táxi conforme guia."))
    });

    private static ChecklistItem I(string id, string instruction, string? field = null, double? value = null) => new(id, instruction, field, value);
    private static ChecklistTemplate T(string id, string scope, params ChecklistItem[] items) => new(id, "2026-10-08.1", scope, Array.AsReadOnly(items));
    private static string Id(string id) => Guid.TryParseExact(id, "N", out _) ? id : throw new ArgumentException("Invalid checklist session id.");
    private string PathFor(string id) => Path.Combine(directory, Id(id) + ".json");

    private FileStream Lock()
    {
        Directory.CreateDirectory(directory);
        try { return new FileStream(Path.Combine(directory, "store.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException exception) { throw new IOException("Checklist store is busy; reload and retry with the current revision.", exception); }
    }

    public ChecklistView Start(string templateId, string flightLabel)
    {
        if (string.IsNullOrWhiteSpace(flightLabel) || flightLabel.Length > 200) throw new ArgumentException("Provide a flight label of 1–200 characters.");
        var template = Templates.SingleOrDefault(t => t.Id == templateId) ?? throw new ArgumentException("Unknown checklist template.");
        using var held = Lock();
        var session = new ChecklistSession(Guid.NewGuid().ToString("N"), flightLabel, template, 0, DateTimeOffset.UtcNow,
            template.Items.Select(i => new ChecklistEntry(i.Id, "pending", null)).ToArray());
        Save(session);
        return View(session);
    }

    public ChecklistView Get(string sessionId)
    {
        using var held = Lock();
        return View(Load(sessionId));
    }

    public object List()
    {
        using var held = Lock();
        return Directory.EnumerateFiles(directory, "*.json").Select(path => JsonSerializer.Deserialize<ChecklistSession>(File.ReadAllText(path))
            ?? throw new IOException("Invalid checklist file.")).OrderByDescending(s => s.StartedAtUtc)
            .Select(s => new { s.Id, s.FlightLabel, TemplateId = s.Template.Id, s.Revision, s.StartedAtUtc }).ToArray();
    }

    public ChecklistView Update(string sessionId, int expectedRevision, string itemId, string status, ChecklistEvidence? evidence)
    {
        if (status is not ("confirmed" or "skipped" or "pending")) throw new ArgumentException("Status must be confirmed, skipped or pending.");
        if (status != "pending" && (evidence is null || string.IsNullOrWhiteSpace(evidence.Note) || evidence.Note.Length > 2000))
            throw new ArgumentException("A confirmation or skip requires an evidence note of 1–2000 characters.");
        if (status == "confirmed" && evidence?.Source is not ("user" or "telemetry")) throw new ArgumentException("Confirmation source must be user or telemetry.");
        if (status == "skipped" && evidence?.Source != "user") throw new ArgumentException("Only an explicit user decision may skip an item.");
        using var held = Lock();
        var session = Load(sessionId);
        if (session.Revision != expectedRevision) throw new InvalidOperationException("Checklist revision conflict; reload before updating.");
        var index = session.Template.Items.ToList().FindIndex(i => i.Id == itemId);
        if (index < 0) throw new ArgumentException("Unknown checklist item.");
        if (status != "pending" && session.Entries.Take(index).Any(e => e.Status == "pending"))
            throw new InvalidOperationException("Earlier checklist items are pending; resolve them or explicitly record a skip first.");
        var entries = session.Entries.ToArray();
        entries[index] = new(itemId, status, status == "pending" ? null : evidence);
        // Reopening a prerequisite invalidates subsequent confirmations; no hidden readiness from an old sequence.
        if (status == "pending")
            for (var i = index + 1; i < entries.Length; i++) entries[i] = new(entries[i].ItemId, "pending", null);
        var updated = session with { Revision = session.Revision + 1, Entries = entries };
        Save(updated);
        return View(updated);
    }

    private ChecklistSession Load(string id) => JsonSerializer.Deserialize<ChecklistSession>(File.ReadAllText(PathFor(id)))
        ?? throw new IOException("Invalid checklist session file.");
    private void Save(ChecklistSession session)
    {
        var path = PathFor(session.Id);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static ChecklistView View(ChecklistSession session) => new(session,
        session.Template.Items.FirstOrDefault(i => session.Entries.Any(e => e.ItemId == i.Id && e.Status == "pending")),
        session.Entries.All(e => e.Status == "confirmed"));
}
