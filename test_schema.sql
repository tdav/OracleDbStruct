-- Тестовый Oracle DDL файл для проверки анализатора зависимостей

-- ====================
-- ТАБЛИЦЫ
-- ====================

-- Базовая таблица без зависимостей
CREATE TABLE departments (
    dept_id NUMBER(10) PRIMARY KEY,
    dept_name VARCHAR2(100) NOT NULL,
    location VARCHAR2(100),
    budget NUMBER(15,2)
);

-- Таблица с зависимостью от departments
CREATE TABLE employees (
    emp_id NUMBER(10) PRIMARY KEY,
    first_name VARCHAR2(50) NOT NULL,
    last_name VARCHAR2(50) NOT NULL,
    email VARCHAR2(100) UNIQUE,
    hire_date DATE DEFAULT SYSDATE,
    salary NUMBER(10,2),
    dept_id NUMBER(10),
    manager_id NUMBER(10),
    CONSTRAINT fk_emp_dept FOREIGN KEY (dept_id)
        REFERENCES departments(dept_id),
    CONSTRAINT fk_emp_manager FOREIGN KEY (manager_id)
        REFERENCES employees(emp_id)
);

-- Таблица проектов
CREATE TABLE projects (
    project_id NUMBER(10) PRIMARY KEY,
    project_name VARCHAR2(200) NOT NULL,
    start_date DATE,
    end_date DATE,
    dept_id NUMBER(10),
    CONSTRAINT fk_proj_dept FOREIGN KEY (dept_id)
        REFERENCES departments(dept_id)
);

-- Таблица связи сотрудников и проектов
CREATE TABLE employee_projects (
    emp_id NUMBER(10),
    project_id NUMBER(10),
    role VARCHAR2(50),
    hours_allocated NUMBER(5,2),
    PRIMARY KEY (emp_id, project_id),
    CONSTRAINT fk_ep_emp FOREIGN KEY (emp_id)
        REFERENCES employees(emp_id),
    CONSTRAINT fk_ep_proj FOREIGN KEY (project_id)
        REFERENCES projects(project_id)
);

-- Таблица истории зарплат
CREATE TABLE salary_history (
    history_id NUMBER(10) PRIMARY KEY,
    emp_id NUMBER(10),
    old_salary NUMBER(10,2),
    new_salary NUMBER(10,2),
    change_date DATE,
    CONSTRAINT fk_sh_emp FOREIGN KEY (emp_id)
        REFERENCES employees(emp_id)
);

-- ====================
-- ПРЕДСТАВЛЕНИЯ (VIEWS)
-- ====================

-- Простое представление
CREATE OR REPLACE VIEW v_employee_details AS
SELECT
    e.emp_id,
    e.first_name,
    e.last_name,
    e.email,
    e.salary,
    d.dept_name,
    d.location
FROM employees e
JOIN departments d ON e.dept_id = d.dept_id;

-- Представление со сложным запросом
CREATE OR REPLACE VIEW v_department_summary AS
SELECT
    d.dept_id,
    d.dept_name,
    COUNT(e.emp_id) as employee_count,
    AVG(e.salary) as avg_salary,
    SUM(e.salary) as total_salary
FROM departments d
LEFT JOIN employees e ON d.dept_id = e.dept_id
GROUP BY d.dept_id, d.dept_name;

-- Представление с несколькими таблицами
CREATE OR REPLACE VIEW v_project_assignments AS
SELECT
    p.project_name,
    e.first_name || ' ' || e.last_name as employee_name,
    ep.role,
    ep.hours_allocated,
    d.dept_name
FROM projects p
JOIN employee_projects ep ON p.project_id = ep.project_id
JOIN employees e ON ep.emp_id = e.emp_id
JOIN departments d ON p.dept_id = d.dept_id;

-- ====================
-- ФУНКЦИИ
-- ====================

-- Функция получения имени сотрудника
CREATE OR REPLACE FUNCTION get_employee_name(p_emp_id NUMBER)
RETURN VARCHAR2
IS
    v_name VARCHAR2(200);
BEGIN
    SELECT first_name || ' ' || last_name
    INTO v_name
    FROM employees
    WHERE emp_id = p_emp_id;

    RETURN v_name;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        RETURN NULL;
END;
/

-- Функция подсчета сотрудников в отделе
CREATE OR REPLACE FUNCTION count_dept_employees(p_dept_id NUMBER)
RETURN NUMBER
IS
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO v_count
    FROM employees
    WHERE dept_id = p_dept_id;

    RETURN v_count;
END;
/

-- ====================
-- ПРОЦЕДУРЫ
-- ====================

-- Процедура повышения зарплаты
CREATE OR REPLACE PROCEDURE raise_salary(
    p_emp_id IN NUMBER,
    p_percent IN NUMBER
)
IS
    v_old_salary NUMBER;
    v_new_salary NUMBER;
BEGIN
    SELECT salary INTO v_old_salary
    FROM employees
    WHERE emp_id = p_emp_id;

    v_new_salary := v_old_salary * (1 + p_percent / 100);

    UPDATE employees
    SET salary = v_new_salary
    WHERE emp_id = p_emp_id;

    INSERT INTO salary_history (history_id, emp_id, old_salary, new_salary, change_date)
    VALUES (salary_history_seq.NEXTVAL, p_emp_id, v_old_salary, v_new_salary, SYSDATE);

    COMMIT;
END;
/

-- Процедура назначения сотрудника на проект
CREATE OR REPLACE PROCEDURE assign_to_project(
    p_emp_id IN NUMBER,
    p_project_id IN NUMBER,
    p_role IN VARCHAR2,
    p_hours IN NUMBER
)
IS
BEGIN
    INSERT INTO employee_projects (emp_id, project_id, role, hours_allocated)
    VALUES (p_emp_id, p_project_id, p_role, p_hours);

    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        UPDATE employee_projects
        SET role = p_role, hours_allocated = p_hours
        WHERE emp_id = p_emp_id AND project_id = p_project_id;
        COMMIT;
END;
/

-- ====================
-- ПАКЕТЫ
-- ====================

-- Пакет для работы с отделами
CREATE OR REPLACE PACKAGE dept_management AS
    PROCEDURE add_department(
        p_dept_name IN VARCHAR2,
        p_location IN VARCHAR2,
        p_budget IN NUMBER
    );

    PROCEDURE delete_department(p_dept_id IN NUMBER);

    FUNCTION get_dept_budget(p_dept_id IN NUMBER) RETURN NUMBER;
END dept_management;
/

CREATE OR REPLACE PACKAGE BODY dept_management AS
    PROCEDURE add_department(
        p_dept_name IN VARCHAR2,
        p_location IN VARCHAR2,
        p_budget IN NUMBER
    ) IS
    BEGIN
        INSERT INTO departments (dept_id, dept_name, location, budget)
        VALUES (dept_seq.NEXTVAL, p_dept_name, p_location, p_budget);
        COMMIT;
    END;

    PROCEDURE delete_department(p_dept_id IN NUMBER) IS
    BEGIN
        DELETE FROM employee_projects
        WHERE emp_id IN (SELECT emp_id FROM employees WHERE dept_id = p_dept_id);

        DELETE FROM employees WHERE dept_id = p_dept_id;
        DELETE FROM projects WHERE dept_id = p_dept_id;
        DELETE FROM departments WHERE dept_id = p_dept_id;

        COMMIT;
    END;

    FUNCTION get_dept_budget(p_dept_id IN NUMBER) RETURN NUMBER IS
        v_budget NUMBER;
    BEGIN
        SELECT budget INTO v_budget
        FROM departments
        WHERE dept_id = p_dept_id;

        RETURN v_budget;
    END;
END dept_management;
/

-- ====================
-- ТРИГГЕРЫ
-- ====================

-- Триггер для аудита изменений зарплаты
CREATE OR REPLACE TRIGGER trg_salary_audit
BEFORE UPDATE OF salary ON employees
FOR EACH ROW
BEGIN
    IF :OLD.salary != :NEW.salary THEN
        INSERT INTO salary_history (history_id, emp_id, old_salary, new_salary, change_date)
        VALUES (salary_history_seq.NEXTVAL, :OLD.emp_id, :OLD.salary, :NEW.salary, SYSDATE);
    END IF;
END;
/

-- Триггер для проверки бюджета отдела
CREATE OR REPLACE TRIGGER trg_check_dept_budget
BEFORE INSERT OR UPDATE ON employees
FOR EACH ROW
DECLARE
    v_total_salary NUMBER;
    v_budget NUMBER;
BEGIN
    SELECT NVL(SUM(salary), 0) INTO v_total_salary
    FROM employees
    WHERE dept_id = :NEW.dept_id
    AND emp_id != :NEW.emp_id;

    SELECT budget INTO v_budget
    FROM departments
    WHERE dept_id = :NEW.dept_id;

    IF v_total_salary + :NEW.salary > v_budget THEN
        RAISE_APPLICATION_ERROR(-20001, 'Превышен бюджет отдела');
    END IF;
END;
/
