import type { AccessMessages } from "../sections/access";
import { plural } from "../plural";

export const access: AccessMessages = {
  emailLabel: "E-mail",
  emailPlaceholder: "you@company.com",
  passwordsDoNotMatch: "Hesla se neshodují.",

  login: {
    title: "Přihlášení do Bugleru",
    description: "Použijte svůj místní účet Bugler.",
    passwordLabel: "Heslo",
    staySignedIn: "Zůstat přihlášen",
    submit: "Přihlásit se",
    submitting: "Přihlašování…",
    forgotPassword: "Zapomněli jste heslo?",
  },

  setup: {
    title: "Vítejte v Bugleru",
    description: "Vytvořte první účet. Stane se správcem serveru.",
    nameLabel: "Jméno",
    passwordLabel: "Heslo (min. 8 znaků)",
    submit: "Vytvořit účet správce",
    submitting: "Vytváření…",
  },

  forgot: {
    title: "Zapomněli jste heslo?",
    description:
      "Zadejte adresu, kterou váš účet používá, a Bugler vám pošle odkaz pro nastavení nového hesla.",
    submit: "Poslat odkaz",
    submitting: "Odesílání…",
    backToSignIn: "Zpět na přihlášení",
    sent: {
      title: "Zkontrolujte si poštu",
      description:
        "Pokud tuto adresu používá některý účet, je k němu na cestě odkaz pro nastavení nového hesla. Odkaz funguje jen jednou a za hodinu přestane platit.",
    },
    nothingArrived: askAgain => <>Nic nedorazilo? Počkejte pár minut a pak {askAgain}.</>,
    askAgain: "požádejte znovu",
  },

  reset: {
    title: "Nastavte nové heslo",
    description:
      "Jakmile bude nastaveno, všechny relace tohoto účtu budou odhlášeny — přihlaste se znovu novým heslem.",
    newPasswordLabel: "Nové heslo (min. 8 znaků)",
    repeatPasswordLabel: "Zopakujte nové heslo",
    submit: "Nastavit heslo",
    submitting: "Nastavování…",
    askForNewLink: "Požádat o nový odkaz",
    missingToken: {
      title: "V tomto odkazu něco chybí",
      description:
        "Tato adresa neobsahuje odkaz pro obnovení hesla. Otevřete odkaz z e-mailu, nebo požádejte o nový.",
      askForLink: "Požádat o odkaz",
    },
  },

  account: {
    title: "Účet",
    description:
      "Jazyk, kterým na vás Bugler mluví, vaše heslo a strojové delegace, které jste vydali, aby " +
      "nástroje mohly číst telemetrii vaším jménem.",
  },

  changePassword: {
    title: "Změna hesla",
    description:
      "Vaše současné heslo potvrzuje, že jste to vy. K přihlášení jinde bude potřeba to nové.",
    changedTitle: "Heslo změněno",
    changedDescription:
      "Všechna ostatní přihlášení tohoto účtu byla odhlášena. Tento prohlížeč zůstává přihlášen.",
    currentPasswordLabel: "Současné heslo",
    newPasswordLabel: "Nové heslo (min. 8 znaků)",
    repeatPasswordLabel: "Zopakujte nové heslo",
    submit: "Změnit heslo",
    submitting: "Probíhá změna…",
  },

  errors: {
    tooManyAttemptsIn: seconds =>
      plural("cs", seconds, {
        one: `Příliš mnoho pokusů. Zkuste to znovu za ${seconds} sekundu.`,
        few: `Příliš mnoho pokusů. Zkuste to znovu za ${seconds} sekundy.`,
        other: `Příliš mnoho pokusů. Zkuste to znovu za ${seconds} sekund.`,
      }),
    tooManyAttempts: "Příliš mnoho pokusů. Chvíli počkejte a zkuste to znovu.",
    invalidCredentials: "Neplatný e-mail nebo heslo.",
    loginFailed: "Přihlášení se nezdařilo.",
    setupAlreadyCompleted: "Počáteční nastavení už bylo dokončeno.",
    setupFailed: "Nastavení se nezdařilo — zkontrolujte e-mail a heslo (min. 8 znaků).",
    passwordNotChanged: "Heslo nebylo změněno.",
    resetUnavailable: "Tento server neumí odesílat poštu, takže hesla nelze obnovit odkazem.",
    forgotRequestFailed: "Žádost se nepodařilo odeslat.",
    passwordNotSet: "Heslo nebylo nastaveno.",
  },
};
