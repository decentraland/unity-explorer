import os
import re
import sys
import time
import base64
import requests
import datetime

URL = f'https://build-api.cloud.unity3d.com/api/v1/orgs/{os.getenv('ORG_ID')}/projects/{os.getenv('PROJECT_ID')}'
POLL_TIME = int(os.getenv('POLL_TIME')) # Seconds

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

headers = create_headers(os.getenv('API_KEY'))

def get_target(target):
    response = requests.get(f'{URL}/buildtargets/{target}', headers=headers)

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

    # Check if the target already exists (re-use of a branch)
    if 'error' in get_target(new_target_name):
        # Create new
        response = requests.post(f'{URL}/buildtargets', headers=headers, json=body)
    else:
        # Update existing
        response = requests.put(f'{URL}/buildtargets/{new_target_name}', headers=headers, json=body)

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
    response = requests.put(f'{URL}/buildtargets/{os.getenv('TARGET')}/envvars', headers=headers, json=body)

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
    response = requests.post(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds', headers=headers, json=body)

    if response.status_code == 202:
        response_json = response.json()
        print("Build started successfully. Response:", response_json)
        return int(response_json[0]['build'])
    else:
        print("Build failed to start with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)
        return -1

def poll_build(id):
    if id == -1:
        print('Error: No build ID known (-1)')
        return

    response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=headers)

    if response.status_code != 200:
        print(f'Failed to poll build with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    response_json = response.json()

    # { created , queued , sentToBuilder , started , restarted , success , failure , canceled , unknown }
    status = response_json['buildStatus']
    match status:
        case 'created' | 'queued' | 'sentToBuilder' | 'started' | 'restarted':
            print(f'Build status: {status}')
            return True
        case 'success':
            print(f'Build finished successfully! | Elapsed (Unity) time: {response_json['totalTimeInSeconds']}')
            return False
        case 'failure' | 'canceled' | 'unknown':
            print(f'Build error! Last known status: "{status}"')
            sys.exit(1)
            return False
        case _:
            print(f'Build status is not known!: "{status}"')
            sys.exit(1)
            return False

# Entrypoint here ->

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

print(f'For more info and live logs, go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')

# Poll the build stats every {POLL_TIME}s
start_time = time.time()
while True:
    if poll_build(id):
        print(f'Runner elapsed time: {datetime.timedelta(seconds=(time.time() - start_time))} | Polling again in {POLL_TIME}s [...]')
        time.sleep(POLL_TIME)
    else:
        print(f'Runner FINAL elapsed time: {time.time() - start_time}')
        break