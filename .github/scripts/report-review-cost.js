// Posts (or updates) the Claude review cost on the PR.
//
// The cost is only known after the run; the claude-code-action writes its
// execution log to $RUNNER_TEMP/claude-execution-output.json, which we parse
// for total_cost_usd / num_turns / subtype. Called from .github/workflows/
// claude-pr-review.yml via actions/github-script:
//
//   const reportCost = require('./.github/scripts/report-review-cost.js');
//   await reportCost({ github, context });
//
// Inputs come from the step env: MODEL (label shown in the comment) and the
// runner-provided RUNNER_TEMP. Run locally by requiring this module and
// passing a stubbed `github`/`context`.
const fs = require('fs');

module.exports = async ({ github, context }) => {
    let cost = null, turns = null, subtype = null;
    try {
        const raw = fs.readFileSync(`${process.env.RUNNER_TEMP}/claude-execution-output.json`, 'utf8');
        let events;
        try { events = JSON.parse(raw); if (!Array.isArray(events)) events = [events]; }
        catch { events = raw.split('\n').filter(l => l.trim()).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean); }
        const result = [...events].reverse().find(e => e && (e.type === 'result' || e.total_cost_usd != null));
        if (result) { cost = result.total_cost_usd; turns = result.num_turns; subtype = result.subtype; }
    } catch (e) { console.log(`Could not read execution cost: ${e.message}`); }

    const MARK = '<!-- review-cost -->';
    const model = process.env.MODEL || 'claude-sonnet-4-6';
    const dollars = (typeof cost === 'number') ? `$${cost.toFixed(2)}` : 'unknown';
    const turnsTxt = (typeof turns === 'number') ? ` · ${turns} turns` : '';
    const note = (subtype && subtype !== 'success') ? ` · ⚠️ did not finish (${subtype})` : '';
    const block = `\n\n${MARK}\n---\n💰 **Review cost:** ${dollars} · \`${model}\`${turnsTxt}${note}`;

    const owner = context.repo.owner, repo = context.repo.repo, issue_number = context.issue.number;
    const comments = await github.paginate(github.rest.issues.listComments, { owner, repo, issue_number, per_page: 100 });
    const claudeComment = comments.filter(c => c.user && c.user.login === 'claude[bot]').pop();

    if (claudeComment) {
        let body = claudeComment.body || '';
        const i = body.indexOf(MARK); // idempotent: drop a prior cost block before re-appending
        if (i !== -1) body = body.substring(0, i).trimEnd();
        try {
            await github.rest.issues.updateComment({ owner, repo, comment_id: claudeComment.id, body: body + block });
            return;
        } catch (e) { console.log(`Could not edit claude comment (${e.message}); posting separately.`); }
    }
    await github.rest.issues.createComment({ owner, repo, issue_number, body: block.replace(/^\n+/, '') });
};
