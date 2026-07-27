import {
  Button,
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "bugler-frontend";
import { RefreshCwIcon } from "lucide-react";

export const Overview = () => (
  <Card className="w-full max-w-md">
    <CardHeader>
      <CardTitle>Ingestion rate</CardTitle>
      <CardDescription>Events accepted in the last 24 hours</CardDescription>
      <CardAction>
        <Button variant="ghost" size="icon-sm" aria-label="Refresh">
          <RefreshCwIcon />
        </Button>
      </CardAction>
    </CardHeader>
    <CardContent>
      <p className="text-sm">
        checkout-service is producing <span className="font-medium">2 340 events/min</span>, up 12% from
        yesterday. No dropped batches in this window.
      </p>
    </CardContent>
    <CardFooter className="gap-2">
      <Button size="sm">View details</Button>
      <Button size="sm" variant="outline">
        Configure alerts
      </Button>
    </CardFooter>
  </Card>
);

export const StatTiles = () => (
  <div className="grid w-full max-w-lg grid-cols-2 gap-4">
    <Card>
      <CardHeader>
        <CardDescription>Errors (24 h)</CardDescription>
        <CardTitle className="text-3xl">1 284</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-muted-foreground text-xs">+8% vs previous day</p>
      </CardContent>
    </Card>
    <Card>
      <CardHeader>
        <CardDescription>P95 trace duration</CardDescription>
        <CardTitle className="text-3xl">412 ms</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-muted-foreground text-xs">across 18 services</p>
      </CardContent>
    </Card>
  </div>
);

export const Minimal = () => (
  <Card className="w-full max-w-sm">
    <CardContent>
      <p className="text-sm">
        Retention purge completed — 41 026 log records older than 30 days were removed.
      </p>
    </CardContent>
  </Card>
);
