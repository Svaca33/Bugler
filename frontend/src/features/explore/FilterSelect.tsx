import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const ALL = "__all__";

/** A nullable single-select: choosing the placeholder entry clears the filter. */
export function FilterSelect(props: {
  placeholder: string;
  value: string | undefined;
  options: { value: string; label: string }[];
  onChange: (value: string | undefined) => void;
}) {
  return (
    <Select
      value={props.value ?? ALL}
      onValueChange={value => props.onChange(value === ALL ? undefined : value)}
    >
      <SelectTrigger className="w-44" size="sm">
        <SelectValue placeholder={props.placeholder} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL}>{props.placeholder}</SelectItem>
        {props.options.map(option => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
