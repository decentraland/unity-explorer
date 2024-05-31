import os
import re
import sys
import time
import shutil
import zipfile
import requests
import datetime
import argparse
# Local
import utils

URL = utils.create_base_url(os.getenv('ORG_ID'), os.getenv('PROJECT_ID'))
HEADERS = utils.create_headers(os.getenv('API_KEY'))
POLL_TIME = int(os.getenv('POLL_TIME', '60')) # Seconds

parser = argparse.ArgumentParser()
parser.add_argument('--resume', help='Resume tracking a running build stored in build_info.json', action='store_true')
parser.add_argument('--cancel', help='Cancel a running build stored in build_info.json', action='store_true')

def get_target(target):
    response = requests.get(f'{URL}/buildtargets/{target}', headers=HEADERS)

    if response.status_code == 200:
        return response.json()
    elif response.status_code == 404:
        print(f'Target "{target}" does not exist (yet?)')
        return response.json()
    else:
        print("Failed to get target data with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

# Some of the code in here won't be used
# Unity does not allow more than 1 item in queue by target
# So we *always* create a new target, no matter what
# by appending the commit's SHA
def clone_current_target():
    body = get_target(os.getenv('TARGET'))
    # Set target name based on branch
    new_target_name = f'{re.sub(r'^t_', '', os.getenv('TARGET'))}-{re.sub('[^A-Za-z0-9]+', '-', os.getenv('BRANCH_NAME'))}'
    # Ensure a new target:
    new_target_name = f'{new_target_name}_{os.getenv('COMMIT_SHA')}'

    body['name'] = new_target_name
    body['settings']['scm']['branch'] = os.getenv('BRANCH_NAME')
    body['settings']['advanced']['unity']['playerExporter']['buildOptions'] = os.getenv('BUILD_OPTIONS').split(',')

    # Copy cache check
    use_cache = os.getenv('USE_CACHE')
    if use_cache == 'true' or use_cache == '':
        body['settings']['buildTargetCopyCache'] = os.getenv('TARGET')
    else:
        if 'buildTargetCopyCache' in body['settings']:
            del body['settings']['buildTargetCopyCache']

    # Check if the target already exists (re-use of a branch)
    if 'error' in get_target(new_target_name):
        # Create new
        response = requests.post(f'{URL}/buildtargets', headers=HEADERS, json=body)
    else:
        # Update existing
        response = requests.put(f'{URL}/buildtargets/{new_target_name}', headers=HEADERS, json=body)

    if response.status_code == 200 or response.status_code == 201:
        os.environ['TARGET'] = new_target_name
    else:
        print('Target failed to clone with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)

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
    response = requests.put(f'{URL}/buildtargets/{os.getenv('TARGET')}/envvars', headers=HEADERS, json=body)

    if response.status_code == 200:
        print("Parameters set successfully. Response:", response.json())
    else:
        print("Parameters failed with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def run_build(branch):
    body = {
        'branch': branch,
        # 'commit': '7d6423555eb96a1e7208adec2b8b7e2f74f1a18f'
    }
    response = requests.post(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds', headers=HEADERS, json=body)

    if response.status_code == 202:
        response_json = response.json()
        print('Build started successfully. Response:', response_json)
        return int(response_json[0]['build'])
    else:
        print('Build failed to start with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)
        return -1

def cancel_build(id):
    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)

    if response.status_code == 204:
        print('Build canceled successfully')
    else:
        print("Build failed to cancel with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def poll_build(id):
    if id == -1:
        print('Error: No build ID known (-1)')
        return

    retries = 0
    max_retries = 5
    wait_time = 2
    while retries < max_retries:
        try:
            response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)
            break
        except Exception as e:
            print(f'Request failed: {e}')
            retries += 1
            if retries < max_retries:
                print(f'Retrying in {wait_time} seconds...')
                time.sleep(wait_time)
                wait_time *= 2  # Increase wait time exponentially for each retry
            else:
                raise Exception(f'Failed after {max_retries} retries')

    if response.status_code != 200:
        print(f'Failed to poll build with ID {id} with status code: {response.status_code}')
        print('Response body:', response.text)
        sys.exit(1)

    response_json = response.json()

    # { created , queued , sentToBuilder , started , restarted , success , failure , canceled , unknown }
    status = response_json['buildStatus']
    match status:
        case 'created' | 'queued' | 'sentToBuilder' | 'started' | 'restarted':
            print(f'Build status: {status}')
            return True
        case 'success':
            print(f'Build finished successfully! | Elapsed (Unity) time: {datetime.timedelta(seconds=(response_json['totalTimeInSeconds']))}')
            return False
        case 'failure' | 'canceled' | 'unknown':
            print(f'Build error! Last known status: "{status}"')
            sys.exit(1)
            return False
        case _:
            print(f'Build status is not known!: "{status}"')
            sys.exit(1)
            return False

def download_artifact(id):
    response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)

    if response.status_code != 200:
        print(f'Failed to get build artifacts with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    response_json = response.json()
    artifact_url = response_json['links']['download_primary']['href']

    download_dir = 'build'
    filepath = os.path.join(download_dir, 'artifact.zip')
    os.makedirs(download_dir, exist_ok=True)

    print('Started downloading artifacts from Unity Cloud...')

    response = requests.get(artifact_url)
    with open(filepath, 'wb') as f:
        f.write(response.content)

    print('Started extracting artifacts from Unity Cloud...')

    with zipfile.ZipFile(filepath, 'r') as zip_ref:
        zip_ref.extractall(download_dir)

    os.remove(filepath)
    print('Artifacts ready!')

# Entrypoint here ->
args = parser.parse_args()

# MODE: Resume
if args.resume or args.cancel:
    build_info = utils.read_build_info()
    if build_info is None:
        sys.exit(1)

    os.environ['TARGET'] = build_info["target"]
    id = build_info["id"]

# MODE: Cancel
    if args.cancel:
        cancel_build(id)

# MODE: Create (default)
else:
    # Clone current target
    # This will clone the current $TARGET and replace the value in $TARGET with it
    # Also sets the branch to $BRANCH_NAME
    clone_current_target()

    # Set ENVs (Parameters)
    # This must run immediately before starting a build
    # to avoid any race conditions with other concurrent builds*
    #
    # *Above warning mostly applies to shared targets, not clones
    set_parameters(get_param_env_variables())

    # Run Build
    id = run_build(os.getenv('BRANCH_NAME'))

    utils.persist_build_info(os.getenv('TARGET'), id)
    print(f'For more info and live logs, go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')

# Poll the build stats every {POLL_TIME}s
start_time = time.time()
while True:
    if poll_build(id):
        print(f'Runner elapsed time: {datetime.timedelta(seconds=(time.time() - start_time))} | Polling again in {POLL_TIME}s [...]')
        time.sleep(POLL_TIME)
    else:
        print(f'Runner FINAL elapsed time: {datetime.timedelta(seconds=(time.time() - start_time))}')
        break

# Handle build artifact
download_artifact(id)

# Cleanup
utils.delete_build_info()