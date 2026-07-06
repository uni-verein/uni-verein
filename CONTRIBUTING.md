# Contributing to uni-verein

First off, thank you for considering contributing to **Uni-verein**! 🎉
It's people like you that make uni-verein such a great tool for university associations worldwide.

This document provides guidelines and steps for contributing. Please read it carefully before making your first contribution.

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
- **Describe the feature in detail** — what should it do and why?
- **Explain the use case** — how would this help university associations?
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
   git commit -m "<issue-number>: add member export to CSV"
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

---

## 🛠️ Development Setup

### Prerequisites

- [Docker Compose](https://docs.docker.com/compose/)
- [.NET 10.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [node.js](https://nodejs.org/en)
- [Git](https://git-scm.com/)

### Setup

> 📌 Comming soon

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

---

### Code Style Frontend

- We use **ESLint** and **Prettier** for code formatting
- Run `npm run lint` before committing
- Always use **single quotes** for strings
- Add meaningful **comments** for complex logic

Your editor should automatically pick up the `.eslintrc` and `.prettierrc` configuration files.

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
