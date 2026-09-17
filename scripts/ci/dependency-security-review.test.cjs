// Execute the real inline workflow scripts with a fake GitHub client. No network.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const workflow = fs.readFileSync(path.join(__dirname, '../../.github/workflows/dependency-security-review.yml'), 'utf8').replace(/\r\n/g, '\n');
const reviewPrompt = fs.readFileSync(path.join(__dirname, '../../.github/prompts/review-instructions.md'), 'utf8');
function script(name) {
  const step = workflow.split('\n      - name: ').find(x => x.startsWith(name + '\n'));
  const body = step.split('          script: |\n')[1].split('\n').filter(x => x.startsWith('            ')).map(x => x.slice(12)).join('\n');
  return new AsyncFunction('github', 'context', 'core', body);
}
const detect = script('Classify changes and maintain label');
const request = script('Request Jarvis through the existing reviewer webhook');
function fixture(files = []) {
  const pr = { number: 42, state: 'open', draft: false, head: { sha: 'a'.repeat(40), ref: 'feat/example' }, base: { sha: 'b'.repeat(40) }, changed_files: files.length, user: { login: 'author' }, labels: [] };
  const requests = [], outputs = {}, operations = [];
  const github = {
    paginate: async () => files,
    rest: {
      pulls: {
        get: async () => ({ data: structuredClone(pr) }),
        listFiles: () => {},
        requestReviewers: async args => { operations.push('request'); requests.push(args); }
      },
      repos: { getCollaboratorPermissionLevel: async () => ({ data: { permission: 'write' } }) },
      issues: {
        addLabels: async () => { operations.push('add'); pr.labels.push({ name: 'new-dependency' }); },
        removeLabel: async () => { operations.push('remove'); pr.labels = pr.labels.filter(label => label.name !== 'new-dependency'); }
      }
    }
  };
  return { pr, requests, outputs, operations, github,
    context: { repo: { owner: 'decentraland', repo: 'unity-explorer' }, payload: { pull_request: structuredClone(pr) } },
    core: { notice() {}, warning() {}, setOutput: (key, value) => { outputs[key] = value } }
  };
}
for (const filename of [
  'Explorer/Packages/manifest.json', 'Explorer/Packages/packages-lock.json',
  'avatar-preview-renderer/Packages/manifest.json', 'avatar-preview-renderer/Packages/packages-lock.json',
  'Explorer/Packages/com.decentraland.embedded/Runtime/Test.cs',
  'avatar-preview-renderer/Packages/com.decentraland.embedded/Editor/Test.cs',
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
    assert.deepEqual(f.operations, ['add']);
  });
}
test('covers renamed and removed files', async () => {
  for (const file of [{ filename: 'moved.txt', previous_filename: 'Explorer/Assets/plugin.dll' }, { filename: 'Explorer/Assets/plugin.dll', status: 'removed' }]) {
    const f = fixture([file]);
    await detect(f.github, f.context, f.core);
    assert.equal(f.outputs.request, 'true');
  }
});
test('leaves an irrelevant inventory unlabeled', async () => {
  const f = fixture([{ filename: 'docs/example.md' }]);
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'false');
  assert.deepEqual(f.operations, []);
});
test('removes a stale label when the current inventory is irrelevant', async () => {
  const f = fixture([{ filename: 'docs/example.md' }]);
  f.pr.labels.push({ name: 'new-dependency' });
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'false');
  assert.deepEqual(f.operations, ['remove']);
  assert.deepEqual(f.pr.labels, []);
});
test('treats a missing stale label as already removed', async () => {
  const f = fixture([{ filename: 'docs/example.md' }]);
  f.pr.labels.push({ name: 'new-dependency' });
  f.github.rest.issues.removeLabel = async () => { const error = new Error('missing'); error.status = 404; throw error; };
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'false');
});
test('fails closed when file inventory is truncated or unavailable', async () => {
  for (const unavailable of [false, true]) {
    const f = fixture([{ filename: 'docs/example.md' }]);
    if (unavailable) f.github.paginate = async () => { throw new Error('API down'); };
    else f.pr.changed_files = 3001;
    await assert.rejects(detect(f.github, f.context, f.core));
    assert.equal(f.outputs.request, undefined);
    assert.deepEqual(f.operations, []);
  }
});
test('leaves drafts pending without requesting the agent', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.pr.draft = true;
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'false');
  assert.deepEqual(f.operations, ['add']);
});
test('ignores stale events', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.pr.head.sha = 'c'.repeat(40);
  await detect(f.github, f.context, f.core);
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.operations, []);
  assert.deepEqual(f.requests, []);
});
test('does not mutate a label when the head changes during classification', async () => {
  const files = [{ filename: 'Explorer/Packages/manifest.json' }];
  const f = fixture(files);
  f.github.paginate = async () => { f.pr.head.sha = 'c'.repeat(40); return files; };
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, undefined);
  assert.deepEqual(f.operations, []);
});
test('updates the label before requesting Jarvis', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  await detect(f.github, f.context, f.core);
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.operations, ['add', 'request']);
});
test('does not repeat an existing relevant label', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.pr.labels.push({ name: 'new-dependency' });
  await detect(f.github, f.context, f.core);
  assert.equal(f.outputs.request, 'true');
  assert.deepEqual(f.operations, []);
});
test('does not request Jarvis when the classifier label is removed after detection', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  await detect(f.github, f.context, f.core);
  f.pr.labels = [];
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.operations, ['add']);
  assert.deepEqual(f.requests, []);
});
test('does not request Jarvis when label maintenance fails', async () => {
  const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
  f.github.rest.issues.addLabels = async () => { throw new Error('labels unavailable'); };
  await assert.rejects(detect(f.github, f.context, f.core));
  assert.equal(f.outputs.request, undefined);
  assert.deepEqual(f.requests, []);
});
test('requests a collaborator PR through the bot reviewer', async () => {
  const f = fixture();
  f.pr.labels.push({ name: 'new-dependency' });
  await request(f.github, f.context, f.core);
  assert.deepEqual(f.requests, [{ owner: 'decentraland', repo: 'unity-explorer', pull_number: 42, reviewers: ['decentraland-bot'] }]);
});
test('requires a maintainer request for external authors or failed authorization', async () => {
  for (const unavailable of [false, true]) {
    const f = fixture();
    f.pr.labels.push({ name: 'new-dependency' });
    f.github.rest.repos.getCollaboratorPermissionLevel = async () => {
      if (unavailable) throw new Error('API down');
      return { data: { permission: 'read' } };
    };
    await request(f.github, f.context, f.core);
    assert.deepEqual(f.requests, []);
  }
});
for (const [name, mutate] of [
  ['a release branch', pr => { pr.head.ref = 'release/2026-09-14' }],
  ['an auto-pr PR', pr => pr.labels.push({ name: 'auto-pr' })]
]) {
  test(`excludes ${name} from AI review`, async () => {
    const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
    mutate(f.pr);
    await detect(f.github, f.context, f.core);
    await request(f.github, f.context, f.core);
    assert.equal(f.outputs.request, 'false');
    assert.deepEqual(f.operations, []);
    assert.deepEqual(f.requests, []);
  });
  test(`strips a stale label from ${name}`, async () => {
    const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
    mutate(f.pr);
    f.pr.labels.push({ name: 'new-dependency' });
    await detect(f.github, f.context, f.core);
    assert.equal(f.outputs.request, 'false');
    assert.deepEqual(f.operations, ['remove']);
  });
  test(`refuses to request ${name} carrying the label`, async () => {
    const f = fixture([{ filename: 'Explorer/Packages/manifest.json' }]);
    mutate(f.pr);
    f.pr.labels.push({ name: 'new-dependency' });
    await request(f.github, f.context, f.core);
    assert.deepEqual(f.requests, []);
  });
}
test('grants the write scope that labelling a pull request requires', () => {
  // `issues: write` does not authorize labels on a PR — the silent 403 that
  // disabled the detailed pass between #10033 and this fix.
  assert.match(workflow, /^ {6}pull-requests: write$/m);
  assert.doesNotMatch(workflow, /^ {6}pull-requests: read$/m);
});
test('leaves security status publication to Agent Server', () => {
  assert.doesNotMatch(workflow, /createCommitStatus|statuses:\s*write/);
});
test('leaves detailed skill activation to the Agent Server snapshot', () => {
  assert.doesNotMatch(reviewPrompt, /security-review\/SKILL\.md|DEPENDENCY_REVIEW/);
});
