import os
import json
import base64

DIR = os.path.dirname(os.path.abspath(__file__))
BUILD_INFO_PATH = os.path.join(DIR, 'build_info.json')

def create_base_url(org, project):
    return f'https://build-api.cloud.unity3d.com/api/v1/orgs/{org}/projects/{project}'

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

def persist_build_info(target, id):
    data = {
        'target': target,
        'id': id
    }
    
    with open(BUILD_INFO_PATH, 'w') as file:
        json.dump(data, file)
        
def read_build_info():
    if not os.path.exists(BUILD_INFO_PATH):
        print('No build_info file found!')
        return None
    
    with open(BUILD_INFO_PATH, 'r') as file:
        data = json.load(file)

    return data

def delete_build_info():
    if not os.path.exists(BUILD_INFO_PATH):
        print('No build_info file found, ignoring delete...')
    
    os.remove(BUILD_INFO_PATH)
    print('build_info file deleted')