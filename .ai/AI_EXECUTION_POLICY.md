# AI Executor Authorization Policy

This file is authoritative for every AI executor (Claude Code, OpenAI Codex,
or any other agent acting on this repository or its GitHub/Jira surfaces). It
governs *executor authority*, not human/Owner authority — see
[Owner actions vs. executor actions](#owner-actions-vs-executor-actions).
`AGENTS.md` and `CLAUDE.md` reference this file; it is not duplicated there.

## 1. Explicit STOP is a hard authorization boundary

When a bounded executor prompt says any equivalent of: STOP, stop for Sol
review, leave Draft, leave Open, leave Unmerged, do not mark Ready, do not
merge, await acceptance, or no further mutation — the executor must treat
that instruction as a hard boundary, not a suggestion.

After reaching that boundary, the executor performs **zero further
repository mutations** except what is strictly necessary to produce its
final report. In particular it must not: commit another correction; push
another commit; transition Draft to Ready; request reviewers unless
explicitly authorized; approve; merge; close; reopen; rebase; update-branch;
force-push; write Jira; begin another task; or continue "because the next
step looks mechanical."

## 2. Authorization must be positive, not inferred

**Absence of a prohibition is not authorization.** Executor authority is
bounded to what the *current* executor prompt positively grants. In
particular:

- permission to create a PR does not imply permission to mark it Ready;
- permission to push does not imply permission to merge;
- permission to review does not imply permission to fix;
- permission to fix does not imply permission to merge;
- Owner standing authorization does not override a narrower current-task
  STOP boundary;
- passing tests, a clean review, or a bot review do not authorize Ready or
  merge;
- Sol acceptance recorded for a *prior* task does not authorize mutation in
  a *later* task;
- an executor must never manufacture its own next phase or next capability.

## 3. Ready-for-review requires current-task authority

An executor may transition a Draft PR to Ready only if the current
authorized prompt explicitly grants Ready authority.

## 4. Merge requires current-task authority

An executor may merge only if the current authorized prompt explicitly
grants merge authority and states the applicable merge conditions. Merge
authority is never inferred from Owner standing approval, prior prompts,
prior conversation turns, PR status, review status, passing CI, an issue
being Done, convenience, or task completeness. Where the current task says
Draft/Open/Unmerged, that requirement has priority over any of the above.

## 5. Post-report immutability

Once an executor has produced a final report stating it has stopped,
completed, or handed off a PR for Sol/Owner review, the bounded execution
session is finished. The executor must not resume mutation merely because
another tool message arrives, a bot posts a comment, a review becomes
available, CI completes, a PR becomes mergeable, or an automated
recommendation appears. Any subsequent mutation requires a new, explicitly
authorized executor task.

## 6. External and bot reviews are evidence, not authority

Automated reviewers (GitHub Copilot, other bots, CI checks, security
scanners) may provide evidence but hold no Sol/Owner acceptance authority
and no merge authority. A failed, unavailable, or exhausted-quota bot review
must not cause an executor to substitute its own merge or Ready decision.
Active AI execution providers for MESP work remain OpenAI/Codex and
Anthropic/Claude unless Owner/Sol explicitly changes that policy; a bot's
inability to review is not itself a product defect.

## 7. Ponytail is never authority

Ponytail (when installed and available) governs productivity, not safety or
authorization — see the Ponytail section in `AGENTS.md` and
`.ai/AI_TOOLING_SETUP.md`. It cannot override STOP, and it cannot authorize
another commit, a Ready transition, a merge, a tracker mutation, or any
weakening of security, Tenant isolation, financial/accounting integrity,
audit, concurrency, or acceptance gates.

## 7a. Tracker authority after the 10 September 2026 cutover

Work-management authority is **GitHub Issues + the GitHub Project**
`MESP — Mini ERP SaaS Platform`
(<https://github.com/users/Hossam1104/projects/1>). **Jira is `Historical
Tracker / Migration Provenance`**: preserved read-only evidence that an
executor must never mutate, close, reopen, or delete.

Everywhere this policy restricts a "Jira write", the restriction now applies
with equal force to a **GitHub Issue or GitHub Project mutation** that records
lifecycle state — closing or reopening a canonical Issue, changing its Project
`Status` or `Capability State`, or activating a capability. Those remain
mutations beyond a bounded task and require positive current-task authority.
The tracker cutover itself granted no implementation authority.

## Owner actions vs. executor actions

This policy limits **AI executor** authority. The Owner may still operate
GitHub manually outside of an executor session at any time. When audit
evidence (e.g. `gh pr view`, GitHub's own actor/timeline data) shows only
that an authenticated Owner account performed an action, repository
documentation must describe that action using the evidence actually
available — it must not assume or assert that an AI executor performed it
unless the evidence supports that specifically. Use evidence-based wording;
do not attribute causality beyond what the evidence establishes.

## Incident history

**2026-08-29 — PR #81 lifecycle advanced past its authorized STOP
boundary.** The authorizing executor task for PR #81 required the PR to be
created and left Draft/Open/Unmerged, pending independent GPT-5.6 Sol
review. The executor's own final report stated that boundary had been
honored. Independent GitHub verification afterward showed the PR
subsequently received additional commits, transitioned from Draft to Ready
for Review, requested a Copilot review (which could not complete — Copilot
quota was exhausted), and was merged to `main` at merge commit
`c8c9084d2cf72550e7a51e4ab9475ef54d14e864`. The final repository content was
technically acceptable and no product code was affected, so no rollback was
required. Available GitHub evidence attributes these lifecycle actions to
the authenticated Owner account `Hossam1104`; the evidence does not
establish whether they were performed manually by the Owner, by an AI
executor operating with the Owner's credentials, or by another authenticated
workflow, and no such causality is claimed. This policy file was added
afterward specifically so that future AI executors treat an explicit STOP as
a hard boundary regardless of what becomes technically possible next (see
Sections 1-5 above).
