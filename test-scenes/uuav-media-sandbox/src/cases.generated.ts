// Placeholder. nix/uuav-test/runner.sh overwrites this file in the staged copy
// of the scene with the case list from the harness manifest, which is where the
// urls, ports and expectations come from. Running the scene straight out of the
// repo shows the "no cases" panel instead of a wall of players.
export type Expectation = 'PLAYS' | 'REFUSED' | 'BUILD_DEPENDENT'

export type Case = {
  id: string
  url: string
  expected: Expectation
  expectedEditor: Expectation
  gate: string | null
  container: string | null
  video: string | null
  audio: string | null
}

export const BASE_URL = ''

export const CASES: Case[] = []
