import { plural } from "../plural";
import type { AlertingMessages } from "../sections/alerting";

/** The nameless hand of a deleted account, as it reads mid-sentence. */
const FORMER = "uživatel, který už tu není";

/** The agent after a verb: "…převzal uživatel Alice" — masculine with "uživatel" on purpose,
 * so the verb agrees with the noun and never guesses at a person's gender. */
const agent = (name: string): string => (name === FORMER ? FORMER : `uživatel ${name}`);

/** The agent opening a sentence, with the relative clause properly closed by its comma. */
const subj = (name: string): string =>
  name === FORMER ? "Uživatel, který už tu není," : `Uživatel ${name}`;

export const alerting: AlertingMessages = {
  page: {
    title: "Epizody",
    subtitle: "Epizody potíží ve službách, které vidíte, a které z nich vám posílají e-mail.",
    episodesTab: "Epizody",
    subscriptionsTab: "Odběry",
  },

  filters: {
    lifecycle: "ŽIVOTNÍ CYKLUS",
    whoIsOnIt: "KDO TO ŘEŠÍ",
    source: "ZDROJ",
    opened: "OTEVŘENO",
    firstLogContains: "PRVNÍ ZÁZNAM OBSAHUJE",
    nobodyYet: "Zatím nikdo",
    acknowledgedByMe: "Převzaté mnou",
    allApplications: "Všechny aplikace",
    allNamespaces: "Všechny namespace",
    allEnvironments: "Všechna prostředí",
    allServices: "Všechny služby",
    anyTime: "Kdykoli",
    searchPlaceholder: "timeout, deadlock…",
    openedPreset: {
      P1D: "Posledních 24 h",
      P7D: "Posledních 7 d",
      P30D: "Posledních 30 d",
    },
    openedPhrase: {
      P1D: "za posledních 24 h",
      P7D: "za posledních 7 d",
      P30D: "za posledních 30 d",
    },
  },

  state: {
    badge: { Open: "otevřená", Quieted: "utichlá", Solved: "vyřešená", Muted: "ztlumená" },
    filterLabel: { Open: "Otevřená", Quieted: "Utichlá", Solved: "Vyřešená", Muted: "Ztlumená" },
  },

  list: {
    columnEpisode: "EPIZODA",
    columnState: "STAV",
    columnDuration: "TRVÁNÍ",
    emptyFiltered: "Filtru stavů neodpovídá žádná epizoda.",
    emptyNoTrouble: "Žádné epizody — žádná hlídaná služba zatím nezaznamenala potíže.",
    loadOlder: "Načíst starší",
    nothingOlder: "Nic staršího",
    loadedCount: count => `načteno ${count}`,
    countOfTotal: (shown, total, window) =>
      `${shown} z ${plural("cs", total, {
        one: `${total} druhu potíží`,
        other: `${total} druhů potíží`,
      })}${window !== undefined ? ` ${window}` : ""}`,
    you: "vy",
    solvedBy: name => `vyřešeno: ${name}`,
    earlierAck: name => `dřívější převzetí: ${name}`,
    hideEarlier: "Skrýt dřívější epizody",
    showEarlier: times =>
      `Tento druh potíží už dříve hořel ${plural("cs", times, {
        one: "jednou",
        other: `${times}krát`,
      })} — zobrazit`,
    mutedDuringEpisode: "hlídka byla během epizody vypnuta",
    errCount: count => `${count} err`,
    warnCount: count => `${count} warn`,
  },

  openNow: {
    title: "Právě otevřené",
    summary: (count, unheld, oldest) =>
      `${plural("cs", count, {
        one: `${count} epizoda`,
        few: `${count} epizody`,
        other: `${count} epizod`,
      })} · ${unheld} zatím bez řešitele · nejstarší ${oldest}`,
    refreshedAgo: ago => `živě · obnoveno před ${ago}`,
    youHold: time => `držíte ji vy · ${time}`,
    heldBy: (name, forTime) => `drží ji ${name} · ${forTime}`,
    nobodyYet: "zatím ji nikdo neřeší",
  },

  recurrence: {
    firstTime: "první výskyt",
    nthTime: n => `${n}. výskyt`,
  },

  version: {
    on: version => `na verzi ${version}`,
    releasedBefore: ago => `vydána ${ago} předtím`,
    releasedTitle: ago => `Vydána ${ago} před otevřením této epizody`,
    runningTitle: "Verze, na které služba běžela, když se epizoda otevřela",
  },

  detail: {
    episodeCaption: "EPIZODA",
    lifecycleCaption: "ŽIVOTNÍ CYKLUS",
    volumeCaption: "DOSAVADNÍ OBJEM",
    notVisible: "Tato epizoda pro vás není viditelná — nebo už neexistuje.",
    openInLogsLink: "Otevřít v lozích ›",
    earlierAckByYou: "Dřívější epizodu tohoto druhu jste převzali vy",
    earlierAckBy: name => `Dřívější epizodu tohoto druhu převzal ${agent(name)}`,
    versionReleasedEarlier: (version, ago) => <>{version} vydána o {ago} dříve</>,
    openedByHealthCheck: "Otevřena health checkem, který přestal odpovídat",
    openedByLog: severity => `Otevřena záznamem ${severity}`,
    sensitivity: words => `citlivost ${words}`,
    sensitivityWords: {
      errorsAndWarnings: "chyby + varování",
      errors: "chyby",
      off: "vypnuto",
    },
    alertMailed: subscribers =>
      `Výstraha odeslána e-mailem ${plural("cs", subscribers, {
        one: `${subscribers} odběrateli`,
        other: `${subscribers} odběratelům`,
      })}`,
    deliveryPending: "čeká na doručení",
    postedToChat: "odesláno do Google Chatu",
    alertPostedToChat: "Výstraha odeslána do Google Chatu",
    stillMatchingLog: since => `Stále přicházejí shody — poslední záznam před ${since}`,
    stillMatchingCheck: since => `Stále trvá — poslední neúspěšný check před ${since}`,
    heldOpenNote: "převzetí ji drží otevřenou — vyřešte ji, nebo vraťte převzetí, ať může utichnout",
    autoCloseNote: minutes => `sama se uzavře po ${minutes} min klidu`,
    errorsCount: count =>
      plural("cs", count, { one: `${count} chyba`, few: `${count} chyby`, other: `${count} chyb` }),
    warningsCount: count => `${count} varování`,
    kindFirstCaption: "TENTO DRUH POTÍŽÍ · POPRVÉ",
    kindEarlierCaption: earlier => `TENTO DRUH POTÍŽÍ · ${earlier}× DŘÍVE`,
    thisOne: "← tato",
    byName: name => name,
    nobody: "nikdo",
    firstOfKind: "První epizoda svého druhu v této službě.",
    cameBack: times =>
      `Stejná služba, stejný druh potíží — vrátil se už ${plural("cs", times, {
        one: "jednou",
        other: `${times}krát`,
      })}.`,
    isHistory: "Tato epizoda je minulost — akce patří nejnovější epizodě svého druhu.",
    openIt: "Otevřít ji ›",
  },

  journal: {
    formerUser: FORMER,
    youAcknowledged: "Epizodu jste převzali",
    acknowledged: name => `Epizodu převzal ${agent(name)}`,
    youTookOver: "Epizodu jste přebrali",
    tookOver: name => `Epizodu přebral ${agent(name)}`,
    youWithdrewYours: "Vrátili jste své převzetí",
    withdrewTheirOwn: name => `${subj(name)} vrátil své převzetí`,
    youWithdrewThe: "Vrátili jste převzetí",
    withdrewThe: name => `${subj(name)} vrátil převzetí`,
    withdrewYours: name => `${subj(name)} vrátil vaše převzetí`,
    youWithdrewOf: holder => `Vrátili jste převzetí uživatele ${holder}`,
    withdrewOf: (name, holder) => `${subj(name)} vrátil převzetí uživatele ${holder}`,
    solvedByYou: "Epizodu jste vyřešili",
    solvedBy: name => `Epizodu vyřešil ${agent(name)}`,
    formerMachine: "neznámý",
    claimed: machine => `Stroj „${machine}“ epizodu převzal`,
    claimRenewed: machine => `Stroj „${machine}“ obnovil svůj lease`,
    claimReleased: machine => `Stroj „${machine}“ epizodu vrátil`,
    claimLapsed: machine => `Převzetí stroje „${machine}“ vypršelo`,
    claimDisplaced: (name, machine) => `${subj(name)} odebral převzetí stroji „${machine}“`,
    notePinned: machine => `Stroj „${machine}“ připnul poznámku`,
    proposalLaid: machine => `Stroj „${machine}“ navrhl: vyřešeno`,
    proposalRejected: (name, machine) => `${subj(name)} zamítl návrh stroje „${machine}“`,
    resigned: machine => `Stroj „${machine}“ se vzdal — tohle opravit neumí`,
    resignationDismissed: (name, machine) => `${subj(name)} smetl rezignaci stroje „${machine}“`,
  },

  machine: {
    badgeClaimed: "drží stroj",
    badgeClaimedTitle: machine => `Epizodu drží stroj: ${machine}`,
    badgeProposal: "návrh: vyřešeno",
    badgeProposalTitle:
      "Stroj věří, že příčina je opravená — potvrďte nebo zamítněte v detailu",
    badgeResigned: "stroj se vzdal",
    badgeResignedTitle:
      "Stroj se podíval a tvrdí, že tohle opravit neumí — je potřeba lidská ruka",
    caption: "STROJNÍ RUKA",
    hand: (machine, holder) => (holder === null ? `„${machine}“` : `„${machine}“ (${holder})`),
    formerHand: "delegace, která už tu není",
    claimHeld: hand => `Epizodu drží stroj ${hand}.`,
    leaseUntil: clockText => `lease běží do ${clockText}`,
    withdrawClaim: "Odebrat převzetí",
    noteCaption: "Poznámka stroje",
    openLink: "Otevřít odkaz ›",
    proposalHeading: "Stroj navrhuje: vyřešeno",
    proposalLaidBy: hand => `Položil ${hand}`,
    openPr: "Otevřít PR ›",
    matchesSince: count =>
      count === 0
        ? "od návrhu žádná shoda"
        : plural("cs", count, {
            one: `${count} shoda od návrhu`,
            few: `${count} shody od návrhu`,
            other: `${count} shod od návrhu`,
          }),
    overtakenNote:
      "Překonáno — potíž se vrátila v novější epizodě, fix tedy nedržel. Návrh už nelze potvrdit.",
    resignationOvertakenNote:
      "Překonáno — druh potíží se vrátil v novější epizodě; tohle prohlášení je historie.",
    confirmSolved: "Potvrdit — vyřešit",
    reject: "Zamítnout",
    resignationHeading: "Stroj se vzdal: tohle opravit neumí",
    resignedBy: hand => `Prohlásil ${hand}`,
    dismiss: "Smést",
    heldOpenNote: "drží ji otevřenou strojní převzetí — utichnout může až po jeho konci",
    rejectFailed: "Zamítnutí se neuložilo.",
    dismissFailed: "Rezignaci se nepodařilo smést.",
    withdrawClaimFailed: "Převzetí se nepodařilo odebrat.",
  },

  reading: {
    caption: "VÝKLAD AI",
    writtenBy: model => `Napsal ${model} — ověřte proti důkazům`,
    pending: "Výklad důkazů se právě píše…",
    failed: "Stroj to tentokrát vzdal; důkazy níže mluví samy za sebe.",
  },

  quietWindow: {
    caption: "TICHÉ OKNO",
    badge: words => `tiché okno ${words}`,
    badgeTitle: "Tento druh potíží si drží vlastní tiché okno místo okna služby",
    days: days =>
      plural("cs", days, { one: `${days} den`, few: `${days} dny`, other: `${days} dní` }),
    inheritedFromService: words => `Zděděno od služby: ${words}.`,
    ownDescription: (own, inherited) =>
      `${own} pro tento druh potíží v této službě — jinak by zdědil ${inherited}.`,
    wholeMinutesOnly: "Pouze celé minuty.",
    bounds: maxMinutes => `Mezi 1 minutou a ${maxMinutes} minutami (7 dní).`,
    notSaved: "Tiché okno se neuložilo.",
    fieldLabel: "Tiché okno pro tento druh potíží, v minutách",
    emptyInherits: "min · prázdné dědí",
  },

  solve: {
    title: "Vyřešit tuto epizodu?",
    verdict: "Vyřešená je verdikt, že příčina byla opravena. Je konečný — vyřešení nelze vzít zpět.",
    stillOpen: lastMatchAgo =>
      `Tato epizoda je stále otevřená (poslední shoda před ${lastMatchAgo}): vyřešením se na místě zavře a pozdější odpovídající záznam otevře novou epizodu s novou výstrahou.`,
    cancel: "Zrušit",
  },

  actions: {
    acknowledge: "Převzít",
    withdraw: "Vrátit převzetí",
    takeOver: "Přebrat",
    solve: "Vyřešit",
    openInLogs: "Otevřít v lozích",
    alreadySolvedNoAck: "Již vyřešena — vyřešenou epizodu nelze převzít.",
    ackNotSaved: "Převzetí se neuložilo.",
    withdrawFailed: "Převzetí nebylo vráceno.",
    alreadySolvedByOther: "Již ji vyřešil někdo jiný.",
    verdictNotSaved: "Verdikt se neuložil.",
  },

  subscriptions: {
    summary: (services, applications) =>
      `Dostáváte e-maily o ${plural("cs", services, {
        one: `${services} službě`,
        other: `${services} službách`,
      })} ${plural("cs", applications, {
        one: `v ${applications} aplikaci`,
        few: `ve ${applications} aplikacích`,
        other: `v ${applications} aplikacích`,
      })}`,
    body: email =>
      `E-mail přijde na ${email}, když odebíraná služba otevře novou epizodu — při uzavření se nic neposílá. Odběr aplikace pokrývá všechny její služby — včetně těch zaregistrovaných později.`,
    accountInbox: "schránku vašeho účtu",
    filterPlaceholder: "Filtrovat aplikace a služby",
    allServicesBadge: "VŠECHNY SLUŽBY, SOUČASNÉ I BUDOUCÍ",
    serviceCount: (subscribed, total) => `${subscribed} Z ${total} SLUŽEB`,
    sensitivityOff: "CITLIVOST VYPNUTA",
    nothingVisible: "Není co odebírat — žádná aplikace pro vás není viditelná.",
    footer:
      "E-mail je vaše vlastní volba; webhook Google Chatu dané aplikace odešle každou epizodu bez ohledu na to, kdo odebírá. Samotná detekce — citlivost a tiché okno — se nastavuje v Administrace → Topologie.",
  },

  help: {
    title: "Jak epizody fungují",
    description:
      "Jedna epizoda je jeden druh potíží v jedné službě — od toho, co ji otevře, po ruku, která ji ukončí.",
    fromTroubleLabel: "OD POTÍŽÍ K VÝSTRAZE",
    step1Title: "Něco se pokazí",
    step1Body: (
      <>
        Chybový záznam — nebo <span className="text-[#DCE8F3]">health check</span>, který třikrát
        po sobě neodpoví.
      </>
    ),
    step2Title: "Dostane otisk",
    step2Body: (
      <>
        Šablona zprávy záznamu s vynechanými proměnnými —{" "}
        <span className="text-[#DCE8F3]">druh potíží</span>.
      </>
    ),
    step3Title: "Otevře se epizoda",
    step3Body: (
      <>
        Jen pokud žádná toho druhu není otevřená. Jinak jen{" "}
        <span className="text-[#DCE8F3]">přiživí</span> tu otevřenou.
      </>
    ),
    step4Title: "Odejde jedna výstraha",
    step4Body: (
      <>
        Odběratelům a do chatu aplikace. <span className="text-[#DCE8F3]">Jednou</span> za
        epizodu, při otevření.
      </>
    ),
    howItEndsLabel: "A JAK KONČÍ",
    lane1Title: "Nikdo si ji nevezme",
    lane1Subtitle: "utichne sama",
    lane1WindowNote: "tiché okno · nic nového",
    quietedMark: "UTICHLÁ",
    lane1NewEpisode: "nová epizoda · znovu e-mail",
    lane2Title: "Převezmete ji",
    lane2Subtitle: "zůstane otevřená, dokud ji nevyřešíte",
    lane2TookOn: "převzali jste ji · 12:44",
    lane2WouldHaveQuieted: "tady by utichla",
    lane2SameEpisode: "stejná epizoda · žádný druhý e-mail",
    fourStatesLabel: "ČTYŘI STAVY",
    stateOpenBody: "Stále přijímá shody.",
    stateQuietedBody: "Utichla sama od sebe. Nikdo netvrdí, že je opraveno.",
    stateSolvedBody: "Někdo rozhodl, že příčina je opravena. Konečné.",
    stateMutedBody: "Hlídka byla v průběhu epizody vypnuta.",
    actionsSummary: (
      <>
        <span className="text-[#DCE8F3]">Převzít</span> znamená vzít si potíže na starost ·{" "}
        <span className="text-[#DCE8F3]">Přebrat</span> je vezme tomu, kdo je drží ·{" "}
        <span className="text-[#DCE8F3]">Vyřešit</span> je ukončí a uvolní všechny ruce. Všechny
        tři dopadají jen na nejnovější epizodu daného druhu.
      </>
    ),
    footer: (
      <>
        Komu chodí e-mail, je vaše vlastní volba v sekci{" "}
        <span className="text-[#A9BDD1]">Odběry</span> · citlivost a tiché okno se nastavují v{" "}
        <span className="text-[#A9BDD1]">Administrace → Topologie</span>
      </>
    ),
    gotIt: "Rozumím",
  },

  format: {
    todayUpper: "DNES",
    yesterdayUpper: "VČERA",
    todayAt: time => `dnes ${time}`,
    ordinal: n => `${n}.`,
  },

  errors: {
    loadEpisodes: "Nepodařilo se načíst epizody",
    loadOpenEpisodes: "Nepodařilo se načíst otevřené epizody",
    countEpisodes: "Nepodařilo se spočítat epizody",
    loadEpisodeHistory: "Nepodařilo se načíst historii epizod",
    loadEpisode: "Nepodařilo se načíst epizodu",
    loadHistory: "Nepodařilo se načíst historii",
    countOpenEpisodes: "Nepodařilo se spočítat otevřené epizody",
    loadSubscriptions: "Nepodařilo se načíst odběry",
    loadSensitivity: "Nepodařilo se načíst citlivost hlídek",
    saveSubscriptions: "Nepodařilo se uložit odběry",
  },
};
