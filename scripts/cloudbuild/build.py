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

# Force a build

URL = utils.create_base_url(os.getenv('ORG_ID'), os.getenv('PROJECT_ID'))
HEADERS = utils.create_headers(os.getenv('API_KEY'))
POLL_TIME = int(os.getenv('POLL_TIME', '60')) # Seconds

build_healthy = True

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
    def generate_body(template_target, name, branch, options, cache):
        body = get_target(template_target)

        body['name'] = name
        body['settings']['scm']['branch'] = branch
        body['settings']['advanced']['unity']['playerExporter']['buildOptions'] = options

        # Copy cache check
        if cache:
            body['settings']['buildTargetCopyCache'] = template_target
        else:
            if 'buildTargetCopyCache' in body['settings']:
                del body['settings']['buildTargetCopyCache']

        return body

    # Set target name based on branch, without commit SHA
    new_target_name = f'{re.sub(r'^t_', '', os.getenv('TARGET'))}-{re.sub('[^A-Za-z0-9]+', '-', os.getenv('BRANCH_NAME'))}'.lower()

    # Generate request body
    # Disabled cache for now as its not playing well with Unity and we delete builds anyway!
    body = generate_body(
        os.getenv('TARGET'),
        new_target_name,
        os.getenv('BRANCH_NAME'),
        os.getenv('BUILD_OPTIONS').split(','),
        false)

    existing_target = get_target(new_target_name)
    
    if 'error' in existing_target:
        # Create new target
        response = requests.post(f'{URL}/buildtargets', headers=HEADERS, json=body)
    else:
        # Target exists, update it
        response = requests.put(f'{URL}/buildtargets/{new_target_name}', headers=HEADERS, json=body)

    if response.status_code == 200 or response.status_code == 201:
        # Override target ENV
        os.environ['TARGET'] = new_target_name
    elif response.status_code == 500 and 'Build target name already in use for this project!' in response.text:
        print('Target update failed due to a possible race condition. Retrying...')
        time.sleep(2)  # Add a small delay before retrying
        clone_current_target()  # Retry the whole process
    else:
        print('Target failed to clone/update with status code:', response.status_code)
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

def get_latest_build(target):
    response = requests.get(f'{URL}/buildtargets/{target}/builds', headers=HEADERS, params={'per_page': 1, 'page': 1})
    
    if response.status_code == 200:
        builds = response.json()
        if builds:
            return builds[0]
    
    print('Failed to get the latest build.')
    return None
    
def run_build(branch):
    max_retries = 10
    retry_delay = 30  # seconds

    for attempt in range(max_retries):
        body = {
            'branch': branch,
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
        sys.exit(1)

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

    global build_healthy
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
            build_healthy = False
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
    try:
        artifact_url = response_json['links']['download_primary']['href']
    except KeyError:
        print(f'Failed to locate any build artifacts - Nothing to download')
        return

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

def download_log(id):
    response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}/log', headers=HEADERS)

    if response.status_code != 200:
        print(f'Failed to get build log with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    with open('unity_cloud_log.log', 'w') as f:
        f.write(response.text)

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
            return True;
    else:
        print('Failed to check running builds on build target with status code:', response.status_code)
        print('Response body:', response.text)
        if trueOnError:
            print('Failover - Assuming at least one running, returning True')
            return True
        else:
            sys.exit(1)

def delete_current_target():
    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}', headers=HEADERS)

    if response.status_code == 204:
        print('Build target deleted successfully')
    else:
        print('Build target failed to be deleted with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)

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
    #
    # If the target already exists, it will check if it has running builds on it
    # If it has running builds, a new target will be created with an added timestamp (Unity can't queue)
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
# Handle build log
download_log(id)

if not build_healthy:
    print(f'Build unhealthy - check the downloaded logs or go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')
    sys.exit(1)

# Cleanup (only if build is healthy)
if get_any_running_builds(os.getenv('TARGET')):
    delete_build(id)
else:
    # Deleting the parent target also removes all builds
    delete_current_target()

utils.delete_build_info()
