import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "bugler-frontend";

/*
 * The table parts are dark-committed chrome like FilterRail: the heading strip and hairline
 * borders are navy hexes tuned for the console theme the app pins itself to, so stories render
 * inside `.dark`. Values go mono automatically — TableCell carries the mono face itself.
 */

export const Default = () => (
  <div className="dark w-full bg-background p-4 text-foreground">
    <div className="overflow-hidden rounded-[11px] border border-[#1E344C] bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-full pl-4">SERVICE</TableHead>
            <TableHead className="text-right">LOGS</TableHead>
            <TableHead className="pr-4 text-right">TRACES</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell className="w-full pl-4 text-xs">Shop · prod · checkout</TableCell>
            <TableCell className="text-right">≈ 1.2 GB</TableCell>
            <TableCell className="pr-4 text-right">≈ 340 MB</TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="w-full pl-4 text-xs">Shop · prod · payments</TableCell>
            <TableCell className="text-right">≈ 810 MB</TableCell>
            <TableCell className="pr-4 text-right">≈ 1.1 GB</TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="w-full pl-4 text-xs">Shop · staging · checkout</TableCell>
            <TableCell className="text-right">≈ 96 MB</TableCell>
            <TableCell className="pr-4 text-right">—</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  </div>
);
