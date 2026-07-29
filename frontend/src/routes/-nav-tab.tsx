import { Link } from "@tanstack/react-router";

/** Active tab is marked by a 2 px underline on the bar's bottom edge, so items run full height. */
export function NavTab(props: { to: string; label: string }) {
  return (
    <Link
      to={props.to}
      className="flex items-center px-3.5 text-[13.5px] text-[#B6C8DA] hover:text-foreground hover:shadow-[inset_0_-2px_0_#2A415C] [&.active]:font-medium [&.active]:text-[#F6C170] [&.active]:shadow-[inset_0_-2px_0_#E9A43C]"
      // A tab points at a page, not at a set of filters: search params must not narrow the
      // match, or a filtered URL such as `/?range=PT1H` would leave Logs unmarked.
      activeOptions={{ exact: props.to === "/", includeSearch: false }}
    >
      {props.label}
    </Link>
  );
}
