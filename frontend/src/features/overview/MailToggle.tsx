/**
 * The subscription state the Alerts › Subscriptions tab owns, readable and settable in place —
 * this is the screen where a reader notices they are not being told about a service that keeps
 * burning. A service covered by an application-wide subscription is shown but not clickable:
 * un-checking it here would mean silently dropping the application subscription, which is a
 * Subscriptions-tab decision.
 */
export function MailToggle(props: {
  subscribed: boolean;
  viaApplication: boolean;
  applicationName: string;
  onToggle: () => void;
}) {
  if (props.viaApplication) {
    return (
      <span
        className="text-primary/70 shrink-0"
        title={`Subscribed through the application ${props.applicationName}`}
        onClick={event => event.stopPropagation()}
      >
        <Envelope filled />
      </span>
    );
  }

  return (
    <button
      type="button"
      className={`shrink-0 ${props.subscribed ? "text-primary" : "text-[#4A6480] hover:text-[#8CA1B8]"}`}
      title={props.subscribed ? "Subscribed — mail when a new episode opens" : "Not subscribed"}
      onClick={event => {
        event.stopPropagation();
        props.onToggle();
      }}
    >
      <Envelope filled={props.subscribed} />
    </button>
  );
}

function Envelope(props: { filled: boolean }) {
  return props.filled ? (
    <svg width="13" height="13" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M1 4.5A1.5 1.5 0 0 1 2.5 3h11A1.5 1.5 0 0 1 15 4.5v.44L8 9.2 1 4.94Z" />
      <path d="M1 6.1v5.4A1.5 1.5 0 0 0 2.5 13h11a1.5 1.5 0 0 0 1.5-1.5V6.1L8.4 10.3a.75.75 0 0 1-.8 0Z" />
    </svg>
  ) : (
    <svg
      width="13"
      height="13"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
      aria-hidden="true"
    >
      <rect x="1.5" y="3.5" width="13" height="9" rx="1.2" />
      <path d="m2 4.5 6 4.2 6-4.2" />
    </svg>
  );
}
