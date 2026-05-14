import os
import stat
import re
import sys
import time
import shutil
import zipfile
import requests
import datetime
import argparse
import collections
from urllib3.util.retry import Retry
from requests.adapters import HTTPAdapter
# Local
import utils

# Exit code that nick-fields/retry watches for in-step retries
RETRYABLE_EXIT_CODE = 99

from zipfile import ZipFile, ZipInfo

class ZipFileWithPermissions(zipfile.ZipFile):
    def _extract_member(self, member, targetpath, pwd):
        if not isinstance(member, zipfile.ZipInfo):
            member = self.getinfo(member)

        targetpath = super()._extract_member(member, targetpath, pwd)

        attr = member.external_attr >> 16
        if attr != 0:
            os.chmod(targetpath, attr)
        return targetpath


# Define whether this is a release workflow based on IS_RELEASE_BUILD
is_release_workflow = os.getenv('IS_RELEASE_BUILD', 'false').lower() == 'true'

URL = utils.create_base_url(os.getenv('ORG_ID'), os.getenv('PROJECT_ID'))
HEADERS = utils.create_headers(os.getenv('API_KEY'))

# Cadence (seconds)
POLL_TIME = int(os.getenv('POLL_TIME', '60'))                   # base poll cadence
QUEUE_POLL_TIME = int(os.getenv('QUEUE_POLL_TIME', '120'))      # throttled cadence while queued and unchanged
STALE_THRESHOLD = int(os.getenv('STALE_POLL_THRESHOLD', '600')) # status unchanged > this -> throttle polling

# Per-phase budgets (seconds). Queue time and active build time are accounted separately
# so a long Unity Cloud queue does not eat into the actual build window.
QUEUE_TIMEOUT = int(os.getenv('QUEUE_TIMEOUT', '14400'))        # default 4h in created/queued/sentToBuilder
BUILD_TIMEOUT = int(os.getenv('BUILD_TIMEOUT', '10800'))        # default 3h once status is started/restarted

# Unity Cloud Build statuses
# https://docs.unity.com/cloud-build/api.html (buildStatus enum)
QUEUE_STATUSES = {'created', 'queued', 'sentToBuilder'}
ACTIVE_STATUSES = {'started', 'restarted'}
TERMINAL_STATUSES = {'success', 'failure', 'canceled', 'unknown'}

build_healthy = True

parser = argparse.ArgumentParser()
parser.add_argument('--resume', help='Resume tracking a running build stored in build_info.json', action='store_true')
parser.add_argument('--cancel', help='Cancel a running build stored in build_info.json', action='store_true')
parser.add_argument('--delete', help='Delete build target after PR is closed or merged', action='store_true')

def validate_branch_name(branch_name):
    #Validates the branch name to ensure it does not contain special characters like +, ., or @."""
    if re.search(r'[+\.@]', branch_name):
        print(f"Error: Branch name '{branch_name}' contains invalid characters (+, ., or @).")
        sys.exit(1)

def resolve_cache_source(template_target: str) -> str:
    t = (template_target or "").lower()
    if t == "t_macos":
        return os.getenv("CACHE_SOURCE_MACOS", "macos-dev")
    if t == "t_windows64":
        return os.getenv("CACHE_SOURCE_WINDOWS", "windows64-dev")
    return template_target

def get_target(target):
    response = requests.get(f'{URL}/buildtargets/{target}', headers=HEADERS)

    print(f'get_target request url: "{URL}/buildtargets/{target}"')

    if response.status_code == 200:
        return response.json()
    elif response.status_code == 404:
        print(f'Target "{target}" does not exist (yet?)')
        return response.json()
    else:
        print("Failed to get target data with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(99)

def clone_current_target(use_cache):
    def generate_body(template_target, name, branch, options, remoteCacheStrategy):
        body = get_target(template_target)

        body['name'] = name
        body['settings']['scm']['branch'] = branch
        body['settings']['advanced']['unity']['playerExporter']['buildOptions'] = options
        body['settings']['remoteCacheStrategy'] = remoteCacheStrategy
        body['settings']['buildSchedule']['isEnabled'] = False

        print(f"Using cache strategy target: {remoteCacheStrategy}")

        # Remove cache for new targets
        if 'buildTargetCopyCache' in body['settings']:
            del body['settings']['buildTargetCopyCache']
        
        # Remove buildtargetid for new targets (unity bug)
        if 'buildtargetid' in body:
            del body['buildtargetid']
        
        return body

    # Set target name based on branch, without commit SHA
    base_target_name  = f'{re.sub(r'^t_', '', os.getenv('TARGET'))}-{re.sub('[^A-Za-z0-9]+', '-', os.getenv('BRANCH_NAME'))}'.lower()

    # Get the install source from the environment variable
    install_source = os.getenv('PARAM_INSTALL_SOURCE', 'launcher')

    # Include install_source in the target name only if it's not 'launcher'
    if install_source and install_source != 'launcher':
        base_target_name = f"{base_target_name}-{install_source}"

    print(f"Start clone_current_target for {base_target_name}")

    if is_release_workflow:
         # Use the tag version in the target name if it's a release workflow
        tag_version = os.getenv('TAG_VERSION', 'unknown-version')
        # Remove dots from the tag version, as unity API does not allow . in the request
        sanitized_tag_version = re.sub(r'\.', '-', tag_version)
        new_target_name = f"{base_target_name}-{sanitized_tag_version}"
    else:
        new_target_name = base_target_name

    print(f"Updated name for target: {new_target_name}")

    template_target = os.getenv('TARGET')

    # Generate request body
    body = generate_body(
        template_target,
        new_target_name,
        os.getenv('BRANCH_NAME'),
        os.getenv('BUILD_OPTIONS').split(','),
        os.getenv('CACHE_STRATEGY'))

    existing_target = get_target(new_target_name)
    
    if 'error' in existing_target:
        print(f"New target found")
        # Create new target with template cache
        if use_cache:
            cache_source = resolve_cache_source(template_target)
            body['settings']['buildTargetCopyCache'] = cache_source
            print(f"Using cache from: {cache_source}")
        else:
            print(f"Not using cache")
        try:
            response = requests.post(f'{URL}/buildtargets', headers=HEADERS, json=body)
        except ConnectionError as e:
            print(f'ConnectionError while trying to post new target: {e}. Retrying...')
            time.sleep(2)  # Add a small delay before retrying
            clone_current_target(use_cache)  # Retry the whole process
    else:
        if use_cache:
            body['settings']['buildTargetCopyCache'] = new_target_name
            print(f"Using existing cache build target: {new_target_name}")
        else:
            print(f"Not using cache")
        try:
            response = requests.put(f'{URL}/buildtargets/{new_target_name}', headers=HEADERS, json=body)
        except ConnectionError as e:
            print(f'ConnectionError while trying to post exisiting target: {e}. Retrying...')
            time.sleep(2)  # Add a small delay before retrying
            clone_current_target(use_cache)  # Retry the whole process

    print(f"clone_current_target response status: {response.status_code}")
    if response.status_code == 200 or response.status_code == 201:
        # Override target ENV
        os.environ['TARGET'] = new_target_name
        print(f"Copying to TARGET env var. {new_target_name}")
    elif response.status_code == 500 and 'Build target name already in use for this project!' in response.text:
        print('Target update failed due to a possible race condition. Retrying...')
        time.sleep(2)  # Add a small delay before retrying
        clone_current_target(True)  # Retry the whole process
    elif response.status_code == 400:
        print('Target update failed due to incompatible cache file. Retrying...')
        time.sleep(2)  # Add a small delay before retrying
        clone_current_target(False)  # Retry the whole process
    else:
        print('Target failed to clone/update with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(99)

def get_param_env_variables():
    param_variables = {}
    for key, value in os.environ.items():
        if key.startswith("PARAM_"):
            # Remove the "PARAM_" prefix from the key
            param_variables[key[len("PARAM_"):]] = value
    return param_variables

def set_parameters(params):
    hardcoded_params = {
        'TEST_ENV_GIT': 'workflowDefault'
    }
    body = hardcoded_params | params
    url = f'{URL}/buildtargets/{os.getenv("TARGET")}/envvars'
    print(f"Request URL: {url}")
    
    response = requests.put(url, headers=HEADERS, json=body)
    
    if response.status_code == 200:
        print("Parameters set successfully. Response:", response.json())
    else:
        print("Parameters failed with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(99)

def get_latest_build(target):
    response = requests.get(f'{URL}/buildtargets/{target}/builds', headers=HEADERS, params={'per_page': 1, 'page': 1})
    
    if response.status_code == 200:
        builds = response.json()
        if builds:
            return builds[0]
    
    print('Failed to get the latest build.')
    return None
    
def run_build(branch, clean):
    max_retries = 10
    retry_delay = 30  # seconds

    print(f'Triggering build for {branch}, clean build = {clean}')
    for attempt in range(max_retries):
        body = {
            'branch': branch,
            'clean' : clean
        }
        try:
            response = requests.post(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds', headers=HEADERS, json=body)

            if response.status_code == 202:
                response_json = response.json()
                print(f'Build response (attempt {attempt + 1}):', response_json)
                
                if 'error' in response_json[0] and 'already a build pending' in response_json[0]['error']:
                    print('A build is already pending. Attempting to cancel it...')
                    latest_build = get_latest_build(os.getenv('TARGET'))
                    if latest_build:
                        cancel_build(latest_build['build'])
                        print(f'Waiting {retry_delay} seconds before retrying...')
                        time.sleep(retry_delay)
                    else:
                        print('Failed to get the latest build ID.')
                        if attempt == max_retries - 1:
                            print('Max retries reached. Exiting.')
                            sys.exit(1)
                elif 'build' in response_json[0]:
                    print('Build started successfully.')
                    return int(response_json[0]['build'])
                else:
                    print('Unexpected response format.')
                    if attempt == max_retries - 1:
                        print('Max retries reached. Exiting.')
                        sys.exit(1)
            else:
                print(f'Build failed to start with status code: {response.status_code}')
                print('Response body:', response.text)
                if attempt == max_retries - 1:
                    print('Max retries reached. Exiting.')
                    sys.exit(1)
        except requests.exceptions.RequestException as e:
            print(f'An exception occurred while trying to start the build (potentially due to a forced socket closure): {e}')
            if attempt == max_retries - 1:
                print('Max retries reached. Exiting.')
                sys.exit(1)
        
        print(f'Retrying... (attempt {attempt + 2} of {max_retries})')
        time.sleep(retry_delay)
    
    print('Failed to start build after maximum retries.')
    sys.exit(1)
    
def cancel_build(id):
    # Best-effort, idempotent: don't fail the cancel path if Unity says the build is already done.
    try:
        check = requests.get(f'{URL}/buildtargets/{os.getenv("TARGET")}/builds/{id}', headers=HEADERS, timeout=30)
        if check.status_code == 200:
            current_status = check.json().get('buildStatus')
            if current_status in TERMINAL_STATUSES:
                print(f'Build {id} already in terminal state ({current_status}). Skipping cancel.')
                return
    except requests.exceptions.RequestException as e:
        print(f'Pre-cancel status check failed ({e}); attempting cancel anyway.')

    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)

    if response.status_code == 204:
        print('Build canceled successfully')
    else:
        print("Build failed to cancel with status code:", response.status_code)
        print("Response body:", response.text)
        # Best-effort; do not abort the cancel step

def poll_build(id):
    if id == -1:
        print('Error: No build ID known (-1)')
        sys.exit(1)
    retries = 0
    max_retries = 5
    wait_time = 2
    while retries < max_retries:
        try:
            response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)
            if response.status_code == 200:
                break
            else:
                print(f'Failed to poll build with ID {id} with status code: {response.status_code}')
                print('Response body:', response.text)
                raise Exception(f"HTTP error {response.status_code}")
        except Exception as e:
            print(f'Request failed: {e}')
            retries += 1
            if retries < max_retries:
                print(f'Retrying in {wait_time} seconds...')
                time.sleep(wait_time)
                wait_time *= 2  # Increase wait time exponentially for each retry
            else:
                print(f'Failed after {max_retries} retries')
                sys.exit(1)
    
    global build_healthy
    response_json = response.json()
    # { created , queued , sentToBuilder , started , restarted , success , failure , canceled , unknown }
    status = response_json['buildStatus']
    match status:
        case 'created' | 'queued' | 'sentToBuilder' | 'started' | 'restarted':
            return True, status, response_json
        case 'success':
            print(f'Build finished successfully! | Elapsed (Unity) time: {datetime.timedelta(seconds=(response_json["totalTimeInSeconds"]))}')
            return False, status, response_json
        case 'failure' | 'canceled' | 'unknown':
            print(f'Build error! Last known status: "{status}"')
            build_healthy = False
            return False, status, response_json
        case _:
            print(f'Build status is not known!: "{status}"')
            sys.exit(1)
            
def download_artifact(id):
    session = requests.Session()
    retries = Retry(
        total=5,              # Retry up to 5 times
        backoff_factor=2,     # Exponential backoff: 2s, 4s, 8s, etc.
        status_forcelist=[502, 503, 504],  # Retry on these HTTP errors
        allowed_methods=["GET"]
    )
    session.mount('https://', HTTPAdapter(max_retries=retries))
    try:
        response = session.get(
            f'{URL}/buildtargets/{os.getenv("TARGET")}/builds/{id}',
            headers=HEADERS, timeout=60
        )
        response.raise_for_status()  # Raise an HTTPError for bad status codes (4xx/5xx)
    except requests.exceptions.RequestException as e:
        print(f'Error: Failed to get build artifacts with ID {id}. Exception: {e}')
        sys.exit(1)

    if response.status_code != 200:
        print(f'Error: Failed to get build artifacts with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text[:500])
        sys.exit(1)
    print('Build artifacts successfully retrieved!')

    response_json = response.json()
    try:
        artifact_url = response_json['links']['download_primary']['href']
    except KeyError:
        print(f'Failed to locate any build artifacts - Nothing to download')
        return

    download_dir = 'build'
    filepath = os.path.join(download_dir, 'artifact.zip')

    # Print current working directory and target download directory
    print(f"Current working directory: {os.getcwd()}")
    print(f"Target download directory: {os.path.join(os.getcwd(), download_dir)}")

    os.makedirs(download_dir, exist_ok=True)

    print(f'Started downloading artifacts from Unity Cloud to {download_dir}...')
    response = requests.get(artifact_url)
    with open(filepath, 'wb') as f:
        f.write(response.content)

    print(f'Started extracting artifacts from Unity Cloud to {download_dir}...')
    try:
        with ZipFileWithPermissions(filepath, 'r') as zip_ref:
            zip_ref.extractall(download_dir)

        # Check if this is a macOS target and verify we have the right permissions set
        if 'macos' in os.getenv('TARGET', '').lower():
            explorer_path = os.path.join(download_dir, 'Decentraland.app', 'Contents', 'MacOS', 'Explorer')
            if os.path.exists(explorer_path):
                is_executable = os.access(explorer_path, os.X_OK)
                print(f"Is Explorer executable? {'Yes' if is_executable else 'No'}")
                print(f"Explorer permissions: {oct(os.stat(explorer_path).st_mode)}")
            else:
                print(f"Warning: Explorer executable not found at {explorer_path}")
        else:
            print("Not a macOS target, skipping Explorer executable check.")

    except zipfile.BadZipFile as e:
        print(f'Failed to unzip the artifact at {filepath}: {e}')
        sys.exit(1)
    except Exception as e:
        print(f'An unexpected error occurred during the extraction: {e}')
        sys.exit(1)

    os.remove(filepath)
    print('Artifacts ready!')

    # Final check to confirm build folder exists
    if os.path.exists(download_dir):
        print(f"Build folder confirmed at: {os.path.join(os.getcwd(), download_dir)}")
    else:
        print(f"ERROR: Build folder not found at expected location: {os.path.join(os.getcwd(), download_dir)}")

def download_log(id):
    with open('unity_cloud_log.log', 'w') as f:
        f.write('Initialize the log file before making the request\n')

    try:
        response = requests.get(
            f'{URL}/buildtargets/{os.getenv("TARGET")}/builds/{id}/log',
            headers=HEADERS, timeout=120, stream=True
        )
    except requests.exceptions.RequestException as e:
        print(f'Warning: Failed to download build log with ID {id}. Exception: {e}')
        print('Continuing without the build log.')
        return  # Gracefully exit without failing the job

    if response.status_code != 200:
        print(f'Warning: Failed to get build log with ID {id} with status code: {response.status_code}')
        print("Response body (partial):", response.text[:500])
        return  # Gracefully exit without failing the job

    try:
        with open('unity_cloud_log.log', 'a') as f:
            for chunk in response.iter_content(chunk_size=1024):
                if chunk:
                    f.write(chunk.decode('utf-8'))
    except requests.exceptions.ChunkedEncodingError as e:
        print(f'Warning: ChunkedEncodingError while writing build log: {e}')
        print('Continuing without completing the build log download.')
    except Exception as e:
        print(f'Warning: Unexpected error while writing build log: {e}')
        print('Continuing without completing the build log download.')
    finally:
        response.close()

    print('Build log ready!')

def delete_build(id):
    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}/artifacts', headers=HEADERS)

    if response.status_code == 200:
        print('Build (on cloud) deleted successfully')
    else:
        print('Build (on cloud) failed to be deleted with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)

def get_any_running_builds(target, trueOnError = True):
    response = requests.get(f'{URL}/buildtargets/{target}/builds?buildStatus=created,queued,sentToBuilder,started,restarted', headers=HEADERS)

    if response.status_code == 200:
        response_json = response.json()
        if response_json == []:
            return False
        else:
            print('Found at least one running build on build target')
            return True
    else:
        print('Failed to check running builds on build target with status code:', response.status_code)
        print('Response body:', response.text)
        if trueOnError:
            print('Failover - Assuming at least one running, returning True')
            return True
        else:
            sys.exit(1)

def delete_current_target():

    # List of targets to delete
    targets = ['macos', 'windows64']
    
    # Loop through each target
    for target in targets:
        base_target_name = f'{target}-{re.sub("[^A-Za-z0-9]+", "-", os.getenv("BRANCH_NAME"))}'.lower()
        response = requests.delete(f'{URL}/buildtargets/{base_target_name}', headers=HEADERS)
        
        if response.status_code == 204:
            print(f'Build target deleted successfully: "{base_target_name}"')
        elif response.status_code == 404:
            print(f'Build target not found: "{base_target_name} - skip deletion"')
        else:
            print('Build target failed to be deleted with status code:', response.status_code)
            print('Response body:', response.text)
            sys.exit(1)
    
    sys.exit(0)

def try_resume_build():
    """If build_info.json points to a still-trackable build, return (target, id, status).

    The persisted record survives across nick-fields/retry attempts within a single
    GitHub Actions step. Reattaching avoids creating a new Unity Cloud build that
    would go to the back of the queue — which is exactly the wrong thing under
    concurrency pressure.
    """
    info = utils.read_build_info()
    if info is None:
        return None

    persisted_target = info.get('target')
    persisted_id = info.get('id')
    if not persisted_target or persisted_id is None:
        utils.delete_build_info()
        return None

    try:
        resp = requests.get(
            f'{URL}/buildtargets/{persisted_target}/builds/{persisted_id}',
            headers=HEADERS,
            timeout=30,
        )
    except requests.exceptions.RequestException as e:
        print(f'Resume probe failed ({e}). Discarding build_info.')
        utils.delete_build_info()
        return None

    if resp.status_code != 200:
        print(f'Resume probe returned status {resp.status_code}. Discarding build_info.')
        utils.delete_build_info()
        return None

    current_status = resp.json().get('buildStatus')
    if current_status in QUEUE_STATUSES or current_status in ACTIVE_STATUSES:
        print(f'Resuming persisted build: target={persisted_target}, id={persisted_id}, status={current_status}')
        return persisted_target, persisted_id, current_status

    print(f'Persisted build status={current_status} - not resumable. Discarding build_info.')
    utils.delete_build_info()
    return None


def write_step_summary(target, build_id, final_status, phase_durations, queue_reasons, queue_elapsed, build_elapsed):
    """Append a phase breakdown to $GITHUB_STEP_SUMMARY (best-effort)."""
    summary_path = os.environ.get('GITHUB_STEP_SUMMARY')
    if not summary_path:
        return

    def fmt(seconds):
        if not seconds:
            return '—'
        return str(datetime.timedelta(seconds=int(seconds)))

    queue_total = sum(phase_durations.get(s, 0) for s in QUEUE_STATUSES)
    build_total = sum(phase_durations.get(s, 0) for s in ACTIVE_STATUSES)

    lines = []
    lines.append('### Unity Cloud Build phase breakdown')
    lines.append('')
    lines.append(f'- Target: `{target}`')
    lines.append(f'- Build ID: `{build_id}`')
    lines.append(f'- Final outcome: `{final_status}`')
    if queue_reasons:
        lines.append(f"- Queue reasons seen: {', '.join(f'`{r}`' for r in sorted(queue_reasons))}")
    lines.append('')
    lines.append('| Phase | Duration | Budget |')
    lines.append('|---|---:|---:|')
    lines.append(f"| created | {fmt(phase_durations.get('created', 0))} | — |")
    lines.append(f"| queued | {fmt(phase_durations.get('queued', 0))} | — |")
    lines.append(f"| sentToBuilder | {fmt(phase_durations.get('sentToBuilder', 0))} | — |")
    lines.append(f"| **queue subtotal** | **{fmt(queue_total or queue_elapsed)}** | {fmt(QUEUE_TIMEOUT)} |")
    lines.append(f"| started | {fmt(phase_durations.get('started', 0))} | — |")
    lines.append(f"| restarted | {fmt(phase_durations.get('restarted', 0))} | — |")
    lines.append(f"| **build subtotal** | **{fmt(build_total or build_elapsed)}** | {fmt(BUILD_TIMEOUT)} |")
    lines.append('')

    try:
        with open(summary_path, 'a') as f:
            f.write('\n'.join(lines) + '\n')
    except OSError as e:
        print(f'Warning: could not write step summary: {e}')


def run_poll_loop(id, build_already_active=False):
    """Polls the build, enforcing queue and build budgets separately.

    Returns (final_outcome: str, phase_durations: dict, queue_reasons: set,
             queue_elapsed: float, build_elapsed: float).
    """
    phase_durations = collections.defaultdict(float)
    queue_reasons = set()

    now = time.time()
    queue_start = now
    build_start = now if build_already_active else None
    last_status = None
    last_status_change = now
    last_poll = now

    while True:
        now = time.time()

        # Attribute the elapsed slice since the last poll to whichever status we last saw.
        if last_status is not None:
            phase_durations[last_status] += now - last_poll
        last_poll = now

        # Enforce phase budgets before polling so a stuck phase cannot loiter past its cap.
        if build_start is None:
            queue_elapsed = now - queue_start
            if queue_elapsed > QUEUE_TIMEOUT:
                print(f'Queue timeout exceeded ({datetime.timedelta(seconds=int(queue_elapsed))} > {datetime.timedelta(seconds=QUEUE_TIMEOUT)}). Cancelling build...')
                cancel_build(id)
                return 'queue_timeout', phase_durations, queue_reasons, queue_elapsed, 0.0
        else:
            build_elapsed = now - build_start
            if build_elapsed > BUILD_TIMEOUT:
                print(f'Build timeout exceeded ({datetime.timedelta(seconds=int(build_elapsed))} > {datetime.timedelta(seconds=BUILD_TIMEOUT)}). Cancelling build...')
                cancel_build(id)
                queue_elapsed = build_start - queue_start
                return 'build_timeout', phase_durations, queue_reasons, queue_elapsed, build_elapsed

        keep_polling, status, response_json = poll_build(id)

        queued_reason = response_json.get('queuedReason')
        if queued_reason and status in QUEUE_STATUSES:
            queue_reasons.add(queued_reason)

        # Detect queue -> active transition once.
        if build_start is None and status in ACTIVE_STATUSES:
            build_start = now
            queue_total_seen = now - queue_start
            print(f'Build picked up by builder after {datetime.timedelta(seconds=int(queue_total_seen))} in queue.')

        if status != last_status:
            queue_elapsed = (build_start or now) - queue_start
            build_elapsed = (now - build_start) if build_start else 0
            reason_suffix = f', queuedReason={queued_reason}' if queued_reason and status in QUEUE_STATUSES else ''
            print(f'Build status: {status} (queue {datetime.timedelta(seconds=int(queue_elapsed))} / build {datetime.timedelta(seconds=int(build_elapsed))}){reason_suffix}')
            last_status = status
            last_status_change = now
        else:
            print(f'Build status: {status}')

        if not keep_polling:
            queue_elapsed = (build_start or now) - queue_start
            build_elapsed = (now - build_start) if build_start else 0
            return status, phase_durations, queue_reasons, queue_elapsed, build_elapsed

        # Adaptive polling: throttle while sitting in queue with no status change.
        if status in QUEUE_STATUSES and (now - last_status_change) > STALE_THRESHOLD:
            poll_interval = QUEUE_POLL_TIME
        else:
            poll_interval = POLL_TIME

        queue_elapsed = (build_start or now) - queue_start
        build_elapsed = (now - build_start) if build_start else 0
        print(f'Runner elapsed: queue {datetime.timedelta(seconds=int(queue_elapsed))} / build {datetime.timedelta(seconds=int(build_elapsed))} | Polling again in {poll_interval}s [...]')
        time.sleep(poll_interval)


# Entrypoint here ->
args = parser.parse_args()

# Tracks whether we attached to an existing in-flight build rather than creating a fresh one.
build_already_active = False

# MODE: Delete
if args.delete:
    delete_current_target()
# MODE: Resume / Cancel — operate on the persisted in-flight build
elif args.resume or args.cancel:
    build_info = utils.read_build_info()
    if build_info is None:
        sys.exit(1)

    os.environ['TARGET'] = build_info["target"]
    id = build_info["id"]

    if args.cancel:
        cancel_build(id)

# MODE: Create (default)
else:

    # Validate branch name before proceeding
    branch_name = os.getenv('BRANCH_NAME')
    validate_branch_name(branch_name)

    # Auto-resume: if a previous attempt in this step left an in-flight build behind
    # (typical when nick-fields/retry fires after a queue timeout), reattach to it
    # instead of POSTing a fresh build that would go to the back of the Unity Cloud queue.
    resumed = try_resume_build()
    if resumed is not None:
        target_name, id, resumed_status = resumed
        os.environ['TARGET'] = target_name
        build_already_active = resumed_status in ACTIVE_STATUSES
    else:
        # Clone current target — clones $TARGET, retargets to $BRANCH_NAME, replaces $TARGET.
        try:
            clone_current_target(True)
        except Exception as e:
            print(f"Operation failed: {e}")

        # Set ENVs (Parameters) immediately before starting the build to avoid race
        # conditions with other concurrent builds on shared targets.
        set_parameters(get_param_env_variables())

        def get_clean_build_bool():
            value = os.getenv('CLEAN_BUILD', 'false').lower()
            if value in ['true', '1']:
                return True
            elif value in ['false', '0']:
                return False
            else:
                raise ValueError(f"Invalid boolean value for CLEAN_BUILD: {value}")

        id = run_build(os.getenv('BRANCH_NAME'), get_clean_build_bool())
        utils.persist_build_info(os.getenv('TARGET'), id)
        print(f'For more info and live logs, go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')

# Poll with separate queue and build budgets
final_outcome, phase_durations, queue_reasons, queue_elapsed, build_elapsed = run_poll_loop(id, build_already_active=build_already_active)
write_step_summary(os.getenv('TARGET'), id, final_outcome, phase_durations, queue_reasons, queue_elapsed, build_elapsed)

if final_outcome in ('queue_timeout', 'build_timeout'):
    # Best-effort: capture whatever Unity has produced so the upload-log step has something to show.
    try:
        download_log(id)
    except Exception as e:
        print(f'Warning: could not download log after timeout: {e}')
    # Exit with the retryable code so nick-fields/retry picks us up. The persisted build_info
    # lets the next attempt reattach via try_resume_build instead of re-queuing.
    sys.exit(RETRYABLE_EXIT_CODE)

print(f'Runner FINAL elapsed: queue {datetime.timedelta(seconds=int(queue_elapsed))} / build {datetime.timedelta(seconds=int(build_elapsed))}')

# Handle build artifact
download_artifact(id)
# Handle build log
download_log(id)

if not build_healthy:
    print(f'Build unhealthy - check the downloaded logs or go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')
    sys.exit(1)

# Cleanup (only if build is healthy and not release)
# We only delete all artifacts, not the build target
if not is_release_workflow:
    delete_build(id)

utils.delete_build_info()
