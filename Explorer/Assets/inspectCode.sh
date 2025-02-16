#!/usr/bin/env bash

# Определяем директорию, где лежит скрипт
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Например, если файл решения (.sln) находится в корне проекта,
# а скрипт лежит в Assets, то можно подняться на уровень выше:
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Затем можно указать путь к файлу решения относительно корня проекта:
SOLUTION_PATH="$PROJECT_ROOT/Explorer.sln"

# Собираем изменённые .cs-файлы в нужных папках относительно корня проекта
CHANGED_FILES=$(git diff --name-only dev...HEAD -- 'Assets/DCL/**/*.cs' 'Assets/Scripts/**/*.cs' | paste -s -d ';' -)

if [ -z "$CHANGED_FILES" ]; then
  echo "Нет изменённых .cs файлов в Assets/DCL или Assets/Scripts по сравнению с веткой dev."
  exit 0
fi

# Запускаем InspectCode с подробным выводом и сохраняем результат в XML
inspectcode "$SOLUTION_PATH" \
  --include="$CHANGED_FILES" \
  --output=InspectCodeResult.xml \
  --verbosity=VERBOSE

echo "Проверка завершена. Результат также сохранён в InspectCodeResult.xml."