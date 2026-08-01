# CI workflow staged outside `.github/workflows/`

`unity-compile.yml` in this directory is a **complete, ready-to-use GitHub Actions workflow**.
It is here rather than in `.github/workflows/` for one reason only:

> `refusing to allow an OAuth App to create or update workflow .github/workflows/unity-compile.yml
> without workflow scope`

The token this repository is pushed with does not carry GitHub's `workflow` scope, so it cannot
create or modify anything under `.github/workflows/`. Committing the file there makes **every**
push fail, including pushes that have nothing to do with CI.

## To activate it

```sh
git mv docs/ci/unity-compile.yml .github/workflows/unity-compile.yml
git commit -m "Enable the Unity client compile workflow"
git push
```

That push has to be made with a credential that has `workflow` scope — a personal access token
with the box ticked, or the GitHub UI's "Add file" button, which is not subject to the same
restriction.

## What it does, and what it still needs

It builds the Unity client so that a broken editor script fails CI instead of surfacing days later
in somebody else's session — see open item 28. It is **gated on a `UNITY_LICENSE` secret**: with no
licence configured the gate job reports "not configured" and the compile is skipped, so the file is
inert and cannot break the existing pipeline.

Two separate things are therefore outstanding, and neither is engineering work:

1. A push credential with `workflow` scope, to move the file into place.
2. `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` in the repository secrets, to make it run.

**It has never executed.** Without a licence it cannot be run even once, so treat the first
licensed run as the real test of it.
