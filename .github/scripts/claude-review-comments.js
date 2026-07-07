// Helpers shared by the Claude review workflows for reading the marker
// comment Claude leaves on a PR and keeping only the newest one visible.
module.exports = {
  // Returns every comment on the PR/issue, oldest first (paginated).
  async fetchIssueComments({ github, context, prNumber }) {
    return github.paginate(github.rest.issues.listComments, {
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: prNumber,
      per_page: 100,
    });
  },

  // Filters to claude[bot] comments containing the given result marker
  // (e.g. 'REVIEW_RESULT:' or 'DEPENDENCY_REVIEW:').
  claudeMarkerComments(comments, marker) {
    return comments.filter(c => c.user?.login === 'claude[bot]' && c.body?.includes(marker));
  },

  // Collapses every comment except the last one as OUTDATED.
  async minimizeAllButLast({ github, comments }) {
    for (const c of comments.slice(0, -1)) {
      await github.graphql(
        `mutation($id: ID!) {
          minimizeComment(input: { subjectId: $id, classifier: OUTDATED }) {
            minimizedComment { isMinimized }
          }
        }`,
        { id: c.node_id }
      ).catch(e => console.log(`minimizeComment ${c.id}: ${e.message}`));
    }
  },
};
