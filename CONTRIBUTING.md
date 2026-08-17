# Contributing to uni-verein

First off, thank you for considering contributing to **Uni-verein**! 🎉
It's people like you that make uni-verein such a great tool for university associations worldwide.

This document provides guidelines and steps for contributing. Please read it carefully before making your first contribution.

By participating in this project, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

---

## 🤝 How Can I Contribute?

### 🐛 Reporting Bugs

Before creating a bug report, please check the [existing issues](https://github.com/uni-verein/uni-verein/issues)
to avoid duplicates.

When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Describe the behavior you observed and what you expected**
- **Include screenshots or screen recordings** if applicable
- **Include your environment details:**
  - OS and version
  - Browser

> 📌 Use the [Bug Report Template](.github/ISSUE_TEMPLATE/bug_report.md) when opening an issue.

---

### 💡 Suggesting Features

Feature suggestions are always welcome! Before submitting:

- Check if the feature has already been [requested](https://github.com/uni-verein/uni-verein/issues)
- Make sure the feature aligns with the project's scope (university club management)

When submitting a feature request:

- **Use a clear and descriptive title**
- **Describe the feature in detail** what should it do and why?
- **Explain the use case** how would this help university associations?
- **Add mockups or examples** if possible

> 📌 Use the [Feature Request Template](.github/ISSUE_TEMPLATE/feature_request.md) when opening an issue.

---

### 🧑‍💻 Your First Code Contribution

Not sure where to start? Look for issues tagged with:

| Label | Description |
|-------|-------------|
| `help wanted` | Issues where we need extra help |
| `bug` | Confirmed bugs that need fixing |
| `enhancement` | New features or improvements |
| `documentation` | Improvements to docs |

You can filter issues by label on the [Issues page](https://github.com/uni-verein/uni-verein/issues).

---

### 🔀 Pull Requests

Follow these steps to submit a pull request:

1. **Clone** the repository
   ```bash
   git clone https://github.com/uni-verein/uni-verein.git
   cd uni-verein
   ```

2. **Create a branch** from `main`
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/your-bug-fix
   ```

3. **Make your changes** and ensure:
   - Your code follows the [style guide](#style-guides)
   - You have written or updated tests where necessary
   - The build succeeds (frontend and backend)
   - All existing tests pass (frontend and backend)
   

4. **Commit your changes** using conventional commit messages (see below)
   ```bash
   git commit -m "feat(members): add CSV export for member list"
   ```

5. **Push to your changes**
   ```bash
   git push origin feat/your-feature-name
   ```

6. **Open a Pull Request** on GitHub:
   - Set the base branch to `main`
   - Fill in the PR template completely
   - Link related issues using `Closes #<issue-number>`
   - Request a review from a maintainer

> ⚠️ Pull requests that do not follow the guidelines may be closed without review.

### 🚦 What happens after you open a PR

None of our CI checks (lint, tests, builds, security scans) run automatically on PRs from
contributors, not even the frontend lint check. That's intentional: `npm ci`/`dotnet
restore` execute whatever code your PR's dependencies bring with them (install scripts,
etc.), so nothing runs against an untrusted PR on our infrastructure before a maintainer
has reviewed it.

A maintainer reviews the PR first and adds the `ready-for-ci` label to trigger the full
pipeline. Only maintainers/collaborators with write access can add labels on GitHub, so
this isn't something you can trigger yourself,  don't worry if you don't see any checks
run right away, that's expected. In the meantime, please run `npm run lint`, `npm run
typecheck`, `npm run format:check` (frontend) and `dotnet build`/`dotnet test` (backend)
locally before opening your PR (see [Development Setup](#-development-setup) below).

---

## 🛠️ Development Setup

### Prerequisites

- [Docker Compose](https://docs.docker.com/compose/)
- [.NET 10.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [node.js](https://nodejs.org/en)
- [Git](https://git-scm.com/)

### Setup

> **Note:** these steps set up the project for local development, which is different from
> the production installation described in [README.md](README.md#-getting-started) (that
> one downloads a released Docker image; this one builds from your working copy).

1. **Clone the repository** (or your fork) and create your `.env` file:
   ```bash
   git clone https://github.com/uni-verein/uni-verein.git
   cd uni-verein
   docker compose -f docker-compose-ini.yml up
   ```
   This is the **recommended** way to create/refresh `.env`, it generates a `.env` with
   random secrets and default DB credentials if one doesn't exist yet, fills in any keys
   that are still missing (without touching ones you've already set), and updates
   `VERSION` to the latest release tag. It's safe to re-run any time, e.g. after pulling
   changes that introduce a new required environment variable.

   Alternatively, you can copy `.env.example` to `.env` by hand and fill in the values
   yourself. The default values work fine for local development, you don't need real
   secrets, just something non-empty.

2. **Start the dev stack** (builds the backend and frontend from source, plus Postgres,
   an nginx reverse proxy, and a fake-SMTP server for testing emails without sending real
   ones):
   ```bash
   docker compose up -d --build
   ```
   - App: [http://localhost:80](http://localhost:80)
   - Fake-SMTP inbox (Papercut): [http://localhost:8080](http://localhost:8080)

   This is the same `docker-compose.yml` our CI pipeline uses to run the Playwright test
   suite, so if it works for you locally it should work in CI too.

3. **(Optional) Fast frontend iteration with hot reload** once the stack above is
   running, you can run the frontend on its own with Vite's dev server instead of
   rebuilding the container on every change:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```

4. **Run the checks before opening a PR**:
   ```bash
   # Frontend (from frontend/)
   npm run lint
   npm run typecheck
   npm run format:check   # or `npm run format` to auto-fix formatting
   npx playwright test    # requires the stack from step 2 to be running

   # Backend (from backend/)
   dotnet format UniVerein.sln --verify-no-changes   # or without --verify-no-changes to auto-fix
   dotnet build UniVerein.sln
   dotnet test UniVerein.sln
   ```

#### Which docker-compose file do I use?

The repository root has several `docker-compose*.yml` files they are **not**
interchangeable. As a contributor, you'll use `docker-compose-ini.yml` (to set up `.env`)
and `docker-compose.yml` (to run the app); the rest exist to support the production
install/update flow documented in `README.md`.

| File | Purpose                                                                                                               | Use for local dev?                       |
|---|-----------------------------------------------------------------------------------------------------------------------|------------------------------------------|
| `docker-compose-ini.yml` | Generates/refreshes `.env` fills in missing secrets and env vars without overwriting existing ones, updates `VERSION` | ✅ Yes (recommended way to set up `.env`) |
| `docker-compose.yml` | Builds backend + frontend from source, Postgres, nginx proxy, fake-SMTP                                               | ✅ Yes (use this to run the app)          |
| `docker-compose-prod-image.yml` | Production stack using pre-built images from GHCR                                                                     | ❌ No                                     |
| `docker-compose-prod.yml` | Production stack, builds from source instead of pulling images                                                        | ❌ No                                     |
| `docker-compose-demo.yml` | Powers the public demo site, restores a demo database on a schedule                                                   | ❌ No                                     |
| `docker-compose-update.yml` | One-shot updater that walks an existing installation through version upgrades                                         | ❌ No                                     |

## 🎨 Style Guides

### Git Commit Messages

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(optional scope): <short description>

[optional body]

[optional footer]
```

**Types:**

| Type | Description |
|------|-------------|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation changes |
| `style` | Code style changes (formatting, missing semicolons, etc.) |
| `refactor` | Code refactoring (no feature change, no bug fix) |
| `test` | Adding or updating tests |
| `chore` | Build process or tooling changes |
| `perf` | Performance improvements |

**Examples:**
```
feat(members): add CSV export for member list
fix(auth): resolve login redirect loop
docs: update installation instructions in README
```

> **A note on ticket prefixes:** you may notice some historical commits or PR titles start
> with an internal ticket ID, e.g. `UV-7 add receipt management`. That comes from the
> maintainers' internal issue tracker (YouTrack) and is **optional, maintainer-only
> metadata** layered on top of Conventional Commits (not a required or alternative)
> format. As an external contributor you don't have access to that tracker and don't need
> it: a plain Conventional Commit like `feat: add receipt management` is fully correct and
> expected.

---

### Code Style Frontend

- We use **ESLint** and **Prettier** for code formatting
- Run `npm run lint` before committing (`npm run lint:fix` auto-fixes what it can)
- Run `npm run format` to auto-format with Prettier, or `npm run format:check` to check
  without writing changes
- Always use **single quotes** for strings
- Add meaningful **comments** for complex logic

Your editor should automatically pick up the `eslint.config.js` and `.prettierrc`
configuration files.

---

### Code Style Backend

- Run `dotnet format UniVerein.sln --verify-no-changes` before committing (drop
  `--verify-no-changes` to auto-fix)
- Follows the style rules in `backend/.editorconfig` (most editors/IDEs pick this up
  automatically)
- `Nullable` reference types are enabled — avoid introducing new nullable warnings

---

### Documentation Style

- Write documentation in **clear, simple English**
- Use **Markdown** for all documentation files
- Use **code blocks** for all commands and code snippets
- Update the docs when you change functionality

---

## 💬 Community

- 💬 **Discussions:** [GitHub Discussions](https://github.com/uni-verein/uni-verein/discussions)
- 🐛 **Issues:** [GitHub Issues](https://github.com/uni-verein/uni-verein/issues)
- 🌐 **Website:** [uni-verein.org](https://uni-verein.org)

---

Thank you for helping make **uni-verein** better for university associations everywhere! 🎓❤️
