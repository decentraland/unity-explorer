// Execute the real inline workflow scripts with a fake GitHub client. No network.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const workflow = fs.readFileSync(path.join(__dirname, '../../.github/workflows/dependency-security-review.yml'), 'utf8');
function script(name) {
  const step = workflow.split('\n      - name: ').find(x => x.startsWith(name + '\n'));
  const body = step.split('          script: |\n')[1].split('\n').filter(x => x.startsWith('            ')).map(x => x.slice(12)).join('\n');
  return new AsyncFunction('github', 'context', 'core', body);
}
const detect = script('Detect changes and initialize status');
const request = script('Request Jarvis through the existing reviewer webhook');
function fixture(files = []) {
  const pr = { number: 42, state: 'open', draft: false, head: { sha: 'a'.repeat(40) }, base: { sha: 'b'.repeat(40) }, changed_files: files.length, user: { login: 'author' } };
  const statuses = [], requests = [], outputs = {};
  const github = {
    paginate: async () => files,
    rest: {
      pulls: { get: async () => ({ data: pr }), listFiles: () => {}, requestReviewers: async args => requests.push(args) },
      repos: { createCommitStatus: async args => statuses.push(args), getCollaboratorPermissionLevel: async () => ({ data: { permission: 'write' } }) },
      issues: { addLabels: async () => {} }
    }
  };
  return { pr, statuses, requests, outputs, github,
    context: { repo: { owner: 'decentraland', repo: 'unity-explorer' }, payload: { pull_request: structuredClone(pr) } },
    core: { notice() {}, warning() {}, setOutput: (key, value) => { outputs[key] = value } }
  };
}
for (const filename of [
  'Explorer/Packages/manifest.json', 'Explorer/Packages/packages-lock.json',
  'avatar-preview-renderer/Packages/manifest.json', 'avatar-preview-renderer/Packages/packages-lock.json',
  'unity-shared-dependencies/package.json', 'unity-shared-dependencies/Runtime/test.cs',
  'avatar-preview-renderer/Assets/Plugins/Test.cs', 'Explorer/Assets/Somewhere/test.dll.meta',
  'Explorer/Assets/Somewhere/libtest.so.1', 'Explorer/Assets/Somewhere/a.bundle/Contents/Info.plist',
  'Explorer/Assets/Somewhere/test.asmdef', 'Explorer/Assets/Somewhere/test.asmref',
  'Explorer/Assets/Editor/Hook.cs', 'scripts/build.sh', '.github/actions/build/action.yml',
  '.github/prompts/test.md', '.claude/skills/test/SKILL.md', 'CLAUDE.md', 'AGENTS.md', '.gitmodules',
  'Explorer/Assets/WebGL/bridge.jslib', 'nested/Cargo.lock', 'nested/package-lock.json'
]) {
  test(`requests review for ${filename}`, async () => {
    const f = fixture([{ filename }]);
    await detect(f.github, f.context, f.core);
    assert.equal(f.outputs.request, 'true');
    assert.equal(f.statuses.at(-1).state, 'pending');
  });
}
test('covers renamed and removed files', async () => {
  for (const file of [{ filename: 'moved.txt', previous_filename: 'Explorer/Assets/plugin.dll' }, { filename: 'Explorer/Assets/plugin.dll', status: 'removed' }]) {
    const f = fixture([file]);
    await detect(f.github, f.context, f.core);
    assert.equal(f.outputs.request, 'true');
  }
});
test('reports success only for a complete irrelevant inventory', async () => {
  const f = fixture([{ filename: 'docs/example.md' }]);
  await detect(f.github, f.context, f.core);
  assert.equal(f.statuses.at(-1).state, 'success');
  assert.equal(f.outputs.request, undefined);
});
test('fails closed when file inventory is truncated or unavailable', async () => {
  for (const unavailable of [false, true]) {
    const f = fixture([{ filename: 'docs/example.md' }]);
    if (unavailable) f.github.paginate = async () => { throw new Error('API down'); };
    else f.pr.changed_files = 3001;
    await assert.rejects(detect(f.github, f.context, f.core));
    assert.equal(f.statuses.at(-1).state, 'failure');
  }
});
test('leaves drafts pending without requesting the agent', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.pr.draft = true;
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'false');
  assert.equal(f.statuses.at(-1).state, 'pending');
});
test('ignores stale events', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.pr.head.sha = 'c'.repeat(40);
  await detect(f.github, f.context, f.core);
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.statuses, []);
  assert.deepEqual(f.requests, []);
});
test('requests a collaborator PR through the bot reviewer', async () => {
  const f = fixture();
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.requests, [{ owner: 'decentraland', repo: 'unity-explorer', pull_number: 42, reviewers: ['decentraland-bot'] }]);
});
test('requires a maintainer request for external authors or failed authorization', async () => {
  for (const unavailable of [false, true]) {
    const f = fixture();
    f.github.rest.repos.getCollaboratorPermissionLevel = async () => {
      if (unavailable) throw new Error('API down');
      return { data: { permission: 'read' } };
    };
    await request(f.github, f.context, f.core);
    assert.deepEqual(f.requests, []);
  }
});
