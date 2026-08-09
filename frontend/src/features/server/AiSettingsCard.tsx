import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import type { components } from "@/api/schema";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SettingsCard } from "@/components/ui/settings-layout";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useT } from "@/i18n";

type AiSettings = components["schemas"]["AiSettingsDto"];
type AiProvider = components["schemas"]["AiProvider"];

const SETTINGS_KEY = ["admin", "ai", "settings"] as const;

const PROVIDERS: AiProvider[] = ["Anthropic", "OpenAiCompatible"];

/** The three-way patience of Alerting's ADR 0009, as the form holds it. */
type PatienceMode = "none" | "seconds" | "forever";

/**
 * The AI provider this Bugler may ask for readings (ADR 0027) — the mail card's twin: stored on
 * the server from the first save, reset returns it to the deployment's configuration, and the
 * test button asks the saved provider outright. Which applications it may see anything from is
 * consent on the application detail, not here (ADR 0028).
 */
export function AiSettingsCard() {
  const t = useT();
  const settings = useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/ai/settings", {});
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load the AI settings.");
      }
      return data;
    },
    // While an admin is typing here, a focus refetch must not clobber the form.
    refetchOnWindowFocus: false,
  });

  return (
    <SettingsCard caption={t.server.ai.caption}>
      {settings.isPending && <p className="text-[12.5px] text-[#8CA1B8]">{t.server.ai.loading}</p>}
      {settings.isError && (
        <p className="text-[12.5px] text-[#F0685A]">{t.server.ai.loadFailed}</p>
      )}

      {settings.data !== undefined && (
        // Remounts whenever the server's answer changes (a save, a reset), which re-seeds the
        // fields and empties the key box; mid-edit refetches never fire (see above).
        <SettingsForm key={formSeed(settings.data)} initial={settings.data} />
      )}
    </SettingsCard>
  );
}

/** Everything the form seeds from — a changed seed means the stored truth moved under us. */
function formSeed(settings: AiSettings): string {
  return [
    settings.source,
    settings.provider,
    settings.baseUrl,
    settings.hasApiKey,
    settings.model,
    settings.patienceSeconds,
  ].join(" ");
}

type ApiKeyEdit = { kind: "keep" } | { kind: "set"; value: string } | { kind: "clear" };

// The generated client types integers as `number | string`; the UI works in numbers.
function patienceModeOf(seconds: number | string | null | undefined): PatienceMode {
  if (seconds === null || seconds === undefined) {
    return "forever";
  }
  return Number(seconds) === 0 ? "none" : "seconds";
}

function SettingsForm(props: { initial: AiSettings }) {
  const t = useT();
  const queryClient = useQueryClient();
  const [provider, setProvider] = useState<AiProvider>(props.initial.provider);
  const [baseUrl, setBaseUrl] = useState(props.initial.baseUrl);
  const [apiKey, setApiKey] = useState<ApiKeyEdit>({ kind: "keep" });
  const [model, setModel] = useState(props.initial.model);
  const [patienceMode, setPatienceMode] = useState<PatienceMode>(
    patienceModeOf(props.initial.patienceSeconds),
  );
  const [patienceSeconds, setPatienceSeconds] = useState(
    patienceModeOf(props.initial.patienceSeconds) === "seconds"
      ? String(Number(props.initial.patienceSeconds))
      : "60",
  );

  const save = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.PUT("/api/admin/ai/settings", {
        body: {
          provider,
          baseUrl,
          // Write-only: null keeps what is stored, an empty string removes it.
          apiKey: apiKey.kind === "keep" ? null : apiKey.kind === "clear" ? "" : apiKey.value,
          model,
          patienceSeconds:
            patienceMode === "forever" ? null : patienceMode === "none" ? 0 : Number(patienceSeconds),
        },
      });
      if (error !== undefined || data === undefined) {
        throw new Error(problemDetail(error) ?? t.server.ai.saveFallback);
      }
      return data;
    },
    onSuccess: data => queryClient.setQueryData(SETTINGS_KEY, data),
  });

  const reset = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.DELETE("/api/admin/ai/settings", {});
      if (error !== undefined || data === undefined) {
        throw new Error(problemDetail(error) ?? t.server.ai.resetFailed);
      }
      return data;
    },
    onSuccess: data => queryClient.setQueryData(SETTINGS_KEY, data),
  });

  const patienceValid =
    patienceMode !== "seconds"
    || (/^\d+$/.test(patienceSeconds)
      && Number(patienceSeconds) >= 0
      && Number(patienceSeconds) <= 3600);
  const canSave =
    model.trim().length > 0
    && patienceValid
    && (provider !== "OpenAiCompatible" || baseUrl.trim().length > 0);
  const dirty =
    provider !== props.initial.provider
    || baseUrl !== props.initial.baseUrl
    || apiKey.kind !== "keep"
    || model !== props.initial.model
    || patienceMode !== patienceModeOf(props.initial.patienceSeconds)
    || (patienceMode === "seconds"
      && patienceSeconds !== String(Number(props.initial.patienceSeconds)));

  return (
    <div className="flex flex-col gap-4">
      <p className="max-w-[62ch] text-[12.5px] text-[#8CA1B8]">{t.server.ai.intro}</p>

      <div className="grid gap-1.5">
        <Label htmlFor="ai-provider">{t.server.ai.providerLabel}</Label>
        <Select value={provider} onValueChange={value => setProvider(value as AiProvider)}>
          <SelectTrigger id="ai-provider" className="w-[340px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {PROVIDERS.map(candidate => (
              <SelectItem key={candidate} value={candidate}>
                {t.server.ai.provider[candidate]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-1.5">
        <Label htmlFor="ai-base-url">{t.server.ai.baseUrlLabel}</Label>
        <Input
          id="ai-base-url"
          value={baseUrl}
          placeholder={provider === "Anthropic" ? "https://api.anthropic.com" : "http://localhost:11434/v1"}
          onChange={event => setBaseUrl(event.currentTarget.value)}
        />
        <p className="text-[11.5px] text-[#7D93AA]">
          {provider === "Anthropic" ? t.server.ai.baseUrlHelpAnthropic : t.server.ai.baseUrlHelpOpenAi}
        </p>
      </div>

      <div className="flex gap-3">
        <div className="grid flex-1 gap-1.5">
          <Label htmlFor="ai-api-key">{t.server.ai.apiKeyLabel}</Label>
          <div className="flex items-center gap-2">
            <Input
              id="ai-api-key"
              type="password"
              autoComplete="new-password"
              className="flex-1"
              value={apiKey.kind === "set" ? apiKey.value : ""}
              placeholder={
                apiKey.kind === "clear"
                  ? t.server.ai.apiKeyRemovedOnSave
                  : props.initial.hasApiKey
                    ? t.server.ai.apiKeySavedKeep
                    : ""
              }
              disabled={apiKey.kind === "clear"}
              onChange={event =>
                // Typing replaces the stored key; erasing it all backs out of the edit.
                // Removing the stored key is the explicit button, never an empty box.
                setApiKey(
                  event.currentTarget.value.length === 0
                    ? { kind: "keep" }
                    : { kind: "set", value: event.currentTarget.value },
                )
              }
            />
            {props.initial.hasApiKey && apiKey.kind === "keep" && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setApiKey({ kind: "clear" })}
              >
                {t.server.ai.removeButton}
              </Button>
            )}
            {apiKey.kind === "clear" && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setApiKey({ kind: "keep" })}
              >
                {t.server.ai.keepButton}
              </Button>
            )}
          </div>
        </div>
        <div className="grid flex-1 gap-1.5">
          <Label htmlFor="ai-model">{t.server.ai.modelLabel}</Label>
          <Input
            id="ai-model"
            value={model}
            placeholder={t.server.ai.modelPlaceholder}
            onChange={event => setModel(event.currentTarget.value)}
          />
        </div>
      </div>

      <div className="grid gap-1.5">
        <Label htmlFor="ai-patience">{t.server.ai.patienceLabel}</Label>
        <div className="flex items-center gap-2">
          <Select
            value={patienceMode}
            onValueChange={value => setPatienceMode(value as PatienceMode)}
          >
            <SelectTrigger id="ai-patience" className="w-[240px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">{t.server.ai.patience.none}</SelectItem>
              <SelectItem value="seconds">{t.server.ai.patience.seconds}</SelectItem>
              <SelectItem value="forever">{t.server.ai.patience.forever}</SelectItem>
            </SelectContent>
          </Select>
          {patienceMode === "seconds" && (
            <Input
              aria-label={t.server.ai.patienceSecondsLabel}
              inputMode="numeric"
              className="w-[90px]"
              value={patienceSeconds}
              aria-invalid={!patienceValid}
              onChange={event => setPatienceSeconds(event.currentTarget.value)}
            />
          )}
        </div>
        <p className="max-w-[62ch] text-[11.5px] text-[#7D93AA]">{t.server.ai.patienceHelp}</p>
      </div>

      <p className="text-[12.5px] text-[#8CA1B8]">
        {props.initial.isConfigured ? t.server.ai.configuredNote : t.server.ai.notConfiguredNote}
      </p>

      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="button"
          size="sm"
          disabled={!canSave || save.isPending}
          onClick={() => save.mutate()}
        >
          {save.isPending ? t.server.ai.savingButton : t.server.ai.saveButton}
        </Button>

        {props.initial.source === "Stored" ? (
          <>
            <span className="text-[12.5px] text-[#8CA1B8]">{t.server.ai.storedNote}</span>
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={reset.isPending}
              onClick={() => reset.mutate()}
            >
              {reset.isPending ? t.server.ai.resettingButton : t.server.ai.resetButton}
            </Button>
          </>
        ) : (
          <span className="text-[12.5px] text-[#8CA1B8]">{t.server.ai.fromConfigurationNote}</span>
        )}
      </div>

      {save.isError && <ErrorNote title={t.server.ai.saveFailedTitle} error={save.error} />}
      {reset.isError && <ErrorNote title={t.server.ai.resetFailed} error={reset.error} />}

      <TestSection dirty={dirty} />
    </div>
  );
}

/**
 * Proves the saved configuration out loud, and reports whatever the provider actually said.
 * Always the saved one: an unsaved form is exactly what a reading would not use either.
 */
function TestSection(props: { dirty: boolean }) {
  const t = useT();
  const ask = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/admin/ai/test", {});
      if (error !== undefined) {
        throw new Error(problemDetail(error) ?? t.server.ai.askRefused);
      }
      return data;
    },
  });

  return (
    <div className="flex flex-col gap-3 border-t border-[#1E344C] pt-4">
      <p className="max-w-[62ch] text-[12.5px] text-[#8CA1B8]">{t.server.ai.testIntro}</p>

      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="button"
          size="sm"
          variant="secondary"
          disabled={ask.isPending}
          onClick={() => ask.mutate()}
        >
          {ask.isPending ? t.server.ai.askingButton : t.server.ai.askTestButton}
        </Button>

        {props.dirty && (
          <span className="text-[12.5px] text-[#8CA1B8]">{t.server.ai.testsSavedNote}</span>
        )}
      </div>

      {ask.isSuccess && ask.data !== undefined && (
        <p className="text-[12.5px] text-[#8CA1B8]">
          {t.server.ai.answerPrefix}{" "}
          <span className="italic">“{ask.data.answer}”</span>{" "}
          <code className="font-mono text-[11.5px]">({ask.data.model})</code>
        </p>
      )}

      {ask.isError && <ErrorNote title={t.server.ai.askFailedTitle} error={ask.error} />}
    </div>
  );
}

function ErrorNote(props: { title: string; error: Error }) {
  return (
    <div className="flex flex-col gap-1 rounded-[9px] border border-[rgba(229,84,74,0.45)] bg-[rgba(229,84,74,0.10)] p-3">
      <p className="text-[12.5px] font-semibold text-[#F0685A]">{props.title}</p>
      {/* The refusal itself: an operator setting this up would otherwise go to the container log for it. */}
      <code className="font-mono text-[11.5px] break-words text-[#F6C7C2]">
        {props.error.message}
      </code>
    </div>
  );
}

/** ProblemDetails carries what actually went wrong; anything else is not worth guessing at. */
function problemDetail(error: unknown): string | undefined {
  if (typeof error !== "object" || error === null) {
    return undefined;
  }

  const problem = error as { detail?: unknown; title?: unknown };
  if (typeof problem.detail === "string") {
    return problem.detail;
  }

  return typeof problem.title === "string" ? problem.title : undefined;
}
