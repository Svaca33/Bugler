// Regenerates src/api/schema.d.ts from the running backend's OpenAPI document.
//
// The document sits behind sign-in everywhere except Development, so this script talks to a
// backend started locally with `dotnet run` — the docker container runs as Production and
// refuses anonymous readers. Whatever goes wrong, it says so and leaves the schema untouched.

const documentUrl = "http://localhost:8080/openapi/v1.json";

let response: Response;
try {
  response = await fetch(documentUrl);
} catch {
  console.error(`Nothing is answering at ${documentUrl}.`);
  console.error("Start the backend first: dotnet run --project src/Bugler.Host (from the repo root).");
  process.exit(1);
}

if (response.status === 401) {
  console.error("The OpenAPI document is behind sign-in outside Development.");
  console.error(
    "The backend on :8080 is likely the docker container (Production). " +
      "Run it locally instead: dotnet run --project src/Bugler.Host (from the repo root).",
  );
  process.exit(1);
}

if (!response.ok) {
  console.error(`Unexpected ${response.status} ${response.statusText} from ${documentUrl}.`);
  process.exit(1);
}

await Bun.write("openapi.json", await response.text());

const generate = Bun.spawnSync(
  ["bun", "x", "openapi-typescript", "openapi.json", "-o", "src/api/schema.d.ts"],
  { stdout: "inherit", stderr: "inherit" },
);
process.exit(generate.exitCode);
