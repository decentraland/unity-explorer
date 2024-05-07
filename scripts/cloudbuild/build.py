import os
import sys
import base64
import requests

URL = f'https://build-api.cloud.unity3d.com/api/v1/orgs/{os.getenv('ORG_ID')}/projects/{os.getenv('PROJECT_ID')}'
URL_TARGET = f'{URL}/buildtargets/{os.getenv('TARGET')}'
URL_BUILD = f'{URL_TARGET}/builds'
URL_ENVVARS = f'{URL_TARGET}/envvars'

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

headers = create_headers(os.getenv('API_KEY'))

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
    response = requests.put(URL_ENVVARS, headers=headers, json=body)

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
    response = requests.post(URL_BUILD, headers=headers, json=body)

    if response.status_code == 202:
        print("Build started successfully. Response:", response.json())
    else:
        print("Build failed to start with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

# Set ENVs (Parameters)
# This must run immediately before starting a build
# to avoid any race conditions with other concurrent builds
set_parameters(get_param_env_variables())

# Run Build
run_build(os.getenv('BRANCH_NAME'))