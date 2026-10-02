#!/usr/bin/env bash
# Usage: mergepr.sh PR  — Ready, update-branch, wait checks, report unresolved threads, merge at head.
set -u
pr=$1; repo=Hossam1104/Mini_ERP_SaaS_Platform
base=$(gh pr view "$pr" --json baseRefName --jq .baseRefName)
[ "$base" = main ] || { echo "$pr base is $base, not main"; exit 3; }
gh pr ready "$pr" >/dev/null 2>&1
out=$(gh pr update-branch "$pr" 2>&1); echo "$pr update: $out"
echo "$out" | grep -qi conflict && { echo "$pr CONFLICT"; exit 2; }
sleep 20
gh pr checks "$pr" --watch --interval 30 >/dev/null 2>&1
gh pr checks "$pr" | cut -f1,2
gh api graphql -f query="{repository(owner:\"Hossam1104\",name:\"Mini_ERP_SaaS_Platform\"){pullRequest(number:$pr){reviewThreads(first:100){nodes{id isResolved path comments(first:1){nodes{body}}}}}}}" \
  --jq '.data.repository.pullRequest.reviewThreads.nodes[]|select(.isResolved|not)|{id,path,b:.comments.nodes[0].body[0:600]}'
head=$(gh pr view "$pr" --json headRefOid --jq .headRefOid)
gh pr merge "$pr" --merge --match-head-commit "$head" 2>&1 | tail -1
echo "$pr $(gh pr view "$pr" --json state --jq .state)"
